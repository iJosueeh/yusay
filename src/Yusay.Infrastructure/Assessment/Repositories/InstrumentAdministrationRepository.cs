using System.Data;
using System.Data.Common;
using Dapper;
using Npgsql;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Assessment.ValueObjects;

namespace Yusay.Infrastructure.Assessment.Repositories;

public sealed class InstrumentAdministrationRepository(IDbConnectionFactory connectionFactory)
    : IInstrumentAdministrationRepository
{
    private const string DuplicateCodeConstraintName = "uq_instrument_code";
    private const string DuplicateVersionNumberConstraintName = "uq_instrument_version_instrument_id_version";

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM yusay.instrument WHERE code = @Code);";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            sql, new { Code = code }, cancellationToken: cancellationToken));
    }

    public async Task<Instrument?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT instrument_id, code, name, description, purpose
            FROM yusay.instrument
            WHERE instrument_id = @InstrumentId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<InstrumentRow>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId }, cancellationToken: cancellationToken));

        return row is null ? null : MapInstrument(row);
    }

    public async Task<Instrument?> GetByIdForUpdateAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT instrument_id, code, name, description, purpose
            FROM yusay.instrument
            WHERE instrument_id = @InstrumentId
            FOR UPDATE;
            """;

        var dbTx = (DbTransaction)transaction;
        var row = await dbTx.Connection!.QuerySingleOrDefaultAsync<InstrumentRow>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId }, dbTx, cancellationToken: cancellationToken));

        return row is null ? null : MapInstrument(row);
    }

    public async Task AddInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.instrument (instrument_id, code, name, description, purpose)
            VALUES (@InstrumentId, @Code, @Name, @Description, @Purpose);
            """;

        var parameters = new
        {
            instrument.InstrumentId,
            Code = instrument.Code.Value,
            instrument.Name,
            instrument.Description,
            instrument.Purpose
        };

        var dbTx = (DbTransaction)transaction;
        try
        {
            await dbTx.Connection!.ExecuteAsync(
                new CommandDefinition(sql, parameters, dbTx, cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(ex.ConstraintName, DuplicateCodeConstraintName, StringComparison.Ordinal))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateCodeConflictMessage);
        }
    }

    public async Task UpdateInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.instrument
            SET name = @Name,
                description = @Description,
                purpose = @Purpose
            WHERE instrument_id = @InstrumentId;
            """;

        var parameters = new
        {
            instrument.InstrumentId,
            instrument.Name,
            instrument.Description,
            instrument.Purpose
        };

        var dbTx = (DbTransaction)transaction;
        await dbTx.Connection!.ExecuteAsync(
            new CommandDefinition(sql, parameters, dbTx, cancellationToken: cancellationToken));
    }

    public async Task<bool> VersionNumberExistsAsync(
        Guid instrumentId,
        int version,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM yusay.instrument_version
                WHERE instrument_id = @InstrumentId AND version = @Version
            );
            """;

        if (transaction != null)
        {
            var dbTx = (DbTransaction)transaction;
            return await dbTx.Connection!.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { InstrumentId = instrumentId, Version = version }, dbTx, cancellationToken: cancellationToken));
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId, Version = version }, cancellationToken: cancellationToken));
    }

    public async Task<int> GetNextVersionNumberAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COALESCE(MAX(version), 0) + 1
            FROM yusay.instrument_version
            WHERE instrument_id = @InstrumentId;
            """;

        var dbTx = (DbTransaction)transaction;
        return await dbTx.Connection!.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { InstrumentId = instrumentId }, dbTx, cancellationToken: cancellationToken));
    }

    public async Task AddVersionAsync(
        InstrumentVersion version,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string insertVersionSql = """
            INSERT INTO yusay.instrument_version (
                instrument_version_id,
                instrument_id,
                version,
                status,
                source_description,
                population,
                administration_conditions,
                license_information,
                limitations
            ) VALUES (
                @InstrumentVersionId,
                @InstrumentId,
                @Version,
                @Status,
                @SourceDescription,
                @Population,
                @AdministrationConditions,
                @LicenseInformation,
                @Limitations
            );
            """;

        const string insertScoringDefSql = """
            INSERT INTO yusay.scoring_definition (instrument_version_id, method)
            VALUES (@InstrumentVersionId, 'SUM');
            """;

        var parameters = new
        {
            version.InstrumentVersionId,
            version.InstrumentId,
            version.Version,
            Status = version.Status.ToDbValue(),
            version.SourceDescription,
            version.Population,
            version.AdministrationConditions,
            version.LicenseInformation,
            version.Limitations
        };

        var dbTx = (DbTransaction)transaction;
        try
        {
            await dbTx.Connection!.ExecuteAsync(
                new CommandDefinition(insertVersionSql, parameters, dbTx, cancellationToken: cancellationToken));

            await dbTx.Connection!.ExecuteAsync(
                new CommandDefinition(insertScoringDefSql, new { version.InstrumentVersionId }, dbTx, cancellationToken: cancellationToken));
        }
        catch (PostgresException ex) when (
            ex.SqlState == PostgresErrorCodes.UniqueViolation &&
            string.Equals(ex.ConstraintName, DuplicateVersionNumberConstraintName, StringComparison.Ordinal))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateVersionConflictMessage);
        }
    }

    public async Task<InstrumentVersion?> GetVersionByIdAsync(
        Guid instrumentVersionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                instrument_version_id,
                instrument_id,
                version,
                status,
                source_description,
                population,
                administration_conditions,
                license_information,
                limitations
            FROM yusay.instrument_version
            WHERE instrument_version_id = @InstrumentVersionId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<InstrumentVersionRow>(
            new CommandDefinition(sql, new { InstrumentVersionId = instrumentVersionId }, cancellationToken: cancellationToken));

        return row is null ? null : MapInstrumentVersion(row);
    }

    public async Task<IReadOnlyList<InstrumentSummaryDto>> ListAdministrativeInstrumentsAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                i.instrument_id AS InstrumentId,
                i.code AS Code,
                i.name AS Name,
                i.description AS Description,
                i.purpose AS Purpose,
                COUNT(iv.instrument_version_id)::int AS TotalVersions,
                MAX(CASE WHEN iv.status = 'PUBLISHED' THEN iv.version ELSE NULL END) AS PublishedVersion
            FROM yusay.instrument i
            LEFT JOIN yusay.instrument_version iv ON iv.instrument_id = i.instrument_id
            GROUP BY i.instrument_id, i.code, i.name, i.description, i.purpose
            ORDER BY i.name ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = await connection.QueryAsync<InstrumentSummaryDto>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return items.ToList();
    }

    public async Task<InstrumentDetailsDto?> GetAdministrativeInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        const string instrumentSql = """
            SELECT instrument_id, code, name, description, purpose
            FROM yusay.instrument
            WHERE instrument_id = @InstrumentId;
            """;

        const string versionsSql = """
            SELECT
                instrument_version_id AS InstrumentVersionId,
                version AS Version,
                status AS Status,
                source_description AS SourceDescription,
                population AS Population,
                administration_conditions AS AdministrationConditions,
                license_information AS LicenseInformation,
                limitations AS Limitations
            FROM yusay.instrument_version
            WHERE instrument_id = @InstrumentId
            ORDER BY version DESC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var instrumentRow = await connection.QuerySingleOrDefaultAsync<InstrumentRow>(
            new CommandDefinition(instrumentSql, new { InstrumentId = instrumentId }, cancellationToken: cancellationToken));

        if (instrumentRow is null)
            return null;

        var versions = (await connection.QueryAsync<InstrumentVersionSummaryDto>(
            new CommandDefinition(versionsSql, new { InstrumentId = instrumentId }, cancellationToken: cancellationToken))).ToList();

        return new InstrumentDetailsDto(
            instrumentRow.Instrument_id,
            instrumentRow.Code,
            instrumentRow.Name,
            instrumentRow.Description,
            instrumentRow.Purpose,
            versions);
    }

    private static Instrument MapInstrument(InstrumentRow row) =>
        Instrument.Rehydrate(
            row.Instrument_id,
            InstrumentCode.Create(row.Code),
            row.Name,
            row.Description,
            row.Purpose);

    private static InstrumentVersion MapInstrumentVersion(InstrumentVersionRow row) =>
        InstrumentVersion.Rehydrate(
            row.Instrument_version_id,
            row.Instrument_id,
            row.Version,
            InstrumentVersionStatusExtensions.FromDbValue(row.Status),
            row.Source_description,
            row.Population,
            row.Administration_conditions,
            row.License_information,
            row.Limitations);

    private sealed class InstrumentRow
    {
        public Guid Instrument_id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
    }

    private sealed class InstrumentVersionRow
    {
        public Guid Instrument_version_id { get; set; }
        public Guid Instrument_id { get; set; }
        public int Version { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Source_description { get; set; } = string.Empty;
        public string Population { get; set; } = string.Empty;
        public string? Administration_conditions { get; set; }
        public string? License_information { get; set; }
        public string Limitations { get; set; } = string.Empty;
    }
}
