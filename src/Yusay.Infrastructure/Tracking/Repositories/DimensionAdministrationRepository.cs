using System.Data.Common;
using Dapper;
using Npgsql;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Infrastructure.Tracking.Repositories;

public sealed class DimensionAdministrationRepository(IDbConnectionFactory connectionFactory)
    : IDimensionAdministrationRepository
{
    private const string DuplicateCodeConstraintName = "uq_dimension_code";
    private const string DuplicateVersionNumberConstraintName =
        "uq_dimension_version_dimension_id_version";

    private const string SingleActiveVersionConstraintName = "uxp_dimension_version_single_active";

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<bool> CodeExistsAsync(
        string code,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM yusay.dimension WHERE code = @Code);";

        if (transaction?.Connection != null)
        {
            return await transaction.Connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                sql, new { Code = code }, transaction, cancellationToken: cancellationToken));
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { Code = code }, cancellationToken: cancellationToken));
    }

    public async Task AddAsync(
        Dimension dimension,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, @Code, @Name, @Description);
            """;

        var parameters = new
        {
            dimension.DimensionId,
            Code = dimension.Code.Value,
            dimension.Name,
            dimension.Description
        };

        try
        {
            if (transaction?.Connection != null)
            {
                await transaction.Connection.ExecuteAsync(new CommandDefinition(
                    sql, parameters, transaction, cancellationToken: cancellationToken));
                return;
            }

            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                sql, parameters, cancellationToken: cancellationToken));
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(exception.ConstraintName, DuplicateCodeConstraintName, StringComparison.Ordinal))
        {
            throw new ConflictException(IDimensionAdministrationRepository.DuplicateCodeConflictMessage);
        }
    }

    public async Task<Dimension?> GetByIdForUpdateAsync(
        Guid dimensionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT dimension_id, code, name, description
            FROM yusay.dimension
            WHERE dimension_id = @DimensionId
            FOR UPDATE;
            """;

        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<DimensionRow>(
            new CommandDefinition(sql, new { DimensionId = dimensionId }, transaction,
                cancellationToken: cancellationToken));

        return row is null
            ? null
            : Dimension.Rehydrate(
                row.Dimension_id,
                (DimensionCode)row.Code,
                row.Name,
                row.Description);
    }

    public async Task UpdateDetailsAsync(
        Dimension dimension,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.dimension
            SET name = @Name, description = @Description
            WHERE dimension_id = @DimensionId;
            """;

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            sql,
            new { dimension.DimensionId, dimension.Name, dimension.Description },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task<bool> VersionNumberExistsAsync(
        Guid dimensionId,
        int version,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM yusay.dimension_version
                WHERE dimension_id = @DimensionId AND version = @Version);
            """;

        return await transaction.Connection!.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { DimensionId = dimensionId, Version = version }, transaction,
            cancellationToken: cancellationToken));
    }

    public async Task AddVersionAsync(
        DimensionVersion version,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string versionSql = """
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@DimensionVersionId, @DimensionId, @Version, @Definition, @MinValue, @MaxValue, @Step, 'DRAFT');
            """;

        var versionParameters = new
        {
            version.DimensionVersionId,
            version.DimensionId,
            version.Version,
            version.Definition,
            version.Scale.MinValue,
            version.Scale.MaxValue,
            version.Scale.Step
        };

        try
        {
            await transaction.Connection!.ExecuteAsync(new CommandDefinition(
                versionSql, versionParameters, transaction, cancellationToken: cancellationToken));

            if (version.Anchors.Count > 0)
            {
                await InsertAnchorsCoreAsync(
                    transaction.Connection!, transaction,
                    version.DimensionVersionId, version.Anchors, cancellationToken);
            }
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(exception.ConstraintName, DuplicateVersionNumberConstraintName,
                StringComparison.Ordinal))
        {
            throw new ConflictException(
                IDimensionAdministrationRepository.DuplicateVersionNumberConflictMessage);
        }
    }

    public async Task<DimensionVersion?> GetVersionForUpdateAsync(
        Guid dimensionId,
        Guid dimensionVersionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string versionSql = """
            SELECT dimension_version_id, dimension_id, version, definition,
                   min_value, max_value, step, status
            FROM yusay.dimension_version
            WHERE dimension_id = @DimensionId
              AND dimension_version_id = @DimensionVersionId
            FOR UPDATE;
            """;

        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<VersionRow>(
            new CommandDefinition(versionSql,
                new { DimensionId = dimensionId, DimensionVersionId = dimensionVersionId },
                transaction, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        const string anchorsSql = """
            SELECT value, label
            FROM yusay.dimension_anchor
            WHERE dimension_version_id = @DimensionVersionId
            ORDER BY value
            FOR UPDATE;
            """;

        var anchors = await transaction.Connection!.QueryAsync<AnchorRow>(
            new CommandDefinition(anchorsSql, new { DimensionVersionId = dimensionVersionId },
                transaction, cancellationToken: cancellationToken));

        return DimensionVersion.Rehydrate(
            row.Dimension_version_id,
            row.Dimension_id,
            row.Version,
            DimensionScale.Create(row.Min_value, row.Max_value, row.Step),
            row.Definition,
            ParseStatus(row.Status),
            anchors.Select(anchor => DimensionAnchor.Create(anchor.Value, anchor.Label)));
    }

    public async Task UpdateVersionDetailsAsync(
        Guid dimensionVersionId,
        string definition,
        int minValue,
        int maxValue,
        int step,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.dimension_version
            SET definition = @Definition,
                min_value = @MinValue,
                max_value = @MaxValue,
                step = @Step
            WHERE dimension_version_id = @DimensionVersionId;
            """;

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            sql,
            new { DimensionVersionId = dimensionVersionId, Definition = definition,
                MinValue = minValue, MaxValue = maxValue, Step = step },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task DeleteAnchorsAsync(
        Guid dimensionVersionId,
        IReadOnlyList<int> values,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM yusay.dimension_anchor
            WHERE dimension_version_id = @DimensionVersionId AND value = ANY(@Values);
            """;

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            sql,
            new { DimensionVersionId = dimensionVersionId, Values = values.ToArray() },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task UpdateAnchorLabelAsync(
        Guid dimensionVersionId,
        int value,
        string label,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.dimension_anchor
            SET label = @Label
            WHERE dimension_version_id = @DimensionVersionId AND value = @Value;
            """;

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            sql,
            new { DimensionVersionId = dimensionVersionId, Value = value, Label = label },
            transaction,
            cancellationToken: cancellationToken));
    }

    public async Task InsertAnchorsAsync(
        Guid dimensionVersionId,
        IReadOnlyList<DimensionAnchor> anchors,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await InsertAnchorsCoreAsync(
            transaction.Connection!, transaction, dimensionVersionId, anchors, cancellationToken);
    }

    public async Task<Guid?> GetActiveVersionIdAsync(
        Guid dimensionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT dimension_version_id
            FROM yusay.dimension_version
            WHERE dimension_id = @DimensionId AND status = 'ACTIVE';
            """;

        return await transaction.Connection!.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            sql, new { DimensionId = dimensionId }, transaction, cancellationToken: cancellationToken));
    }

    public async Task<bool> UpdateStatusAsync(
        Guid dimensionVersionId,
        DimensionVersionStatus status,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.dimension_version
            SET status = @Status
            WHERE dimension_version_id = @DimensionVersionId;
            """;

        try
        {
            var affected = await transaction.Connection!.ExecuteAsync(new CommandDefinition(
                sql,
                new { DimensionVersionId = dimensionVersionId, Status = ToStatusString(status) },
                transaction,
                cancellationToken: cancellationToken));

            return affected == 1;
        }
        catch (PostgresException exception) when (
            exception.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(exception.ConstraintName, SingleActiveVersionConstraintName,
                StringComparison.Ordinal))
        {
            throw new ConflictException(
                IDimensionAdministrationRepository.AnotherActiveVersionConflictMessage);
        }
    }

    private static async Task InsertAnchorsCoreAsync(
        DbConnection connection,
        DbTransaction transaction,
        Guid dimensionVersionId,
        IReadOnlyList<DimensionAnchor> anchors,
        CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label)
            SELECT @DimensionVersionId, values_to_insert.v, values_to_insert.l
            FROM unnest(@Values, @Labels) AS values_to_insert(v, l);
            """;

        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                DimensionVersionId = dimensionVersionId,
                Values = anchors.Select(anchor => anchor.Value).ToArray(),
                Labels = anchors.Select(anchor => anchor.Label).ToArray()
            },
            transaction,
            cancellationToken: cancellationToken));
    }

    private static string ToStatusString(DimensionVersionStatus status) => status switch
    {
        DimensionVersionStatus.Draft => "DRAFT",
        DimensionVersionStatus.Active => "ACTIVE",
        DimensionVersionStatus.Retired => "RETIRED",
        _ => throw new InvalidOperationException(
            $"Estado de versión no reconocido para la escritura en yusay.dimension_version: '{status}'.")
    };

    private static DimensionVersionStatus ParseStatus(string status) => status switch
    {
        "DRAFT" => DimensionVersionStatus.Draft,
        "ACTIVE" => DimensionVersionStatus.Active,
        "RETIRED" => DimensionVersionStatus.Retired,
        _ => throw new InvalidOperationException(
            $"Valor de estado no reconocido en yusay.dimension_version: '{status}'.")
    };

    private sealed class DimensionRow
    {
        public Guid Dimension_id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    private sealed class VersionRow
    {
        public Guid Dimension_version_id { get; init; }
        public Guid Dimension_id { get; init; }
        public int Version { get; init; }
        public string Definition { get; init; } = string.Empty;
        public int Min_value { get; init; }
        public int Max_value { get; init; }
        public int Step { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    private sealed class AnchorRow
    {
        public int Value { get; init; }
        public string Label { get; init; } = string.Empty;
    }
}
