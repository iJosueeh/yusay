using System.Data.Common;
using Dapper;
using Npgsql;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Infrastructure.Tracking.Repositories;

// B5a de RF-021: alta y edición de Dimension con Dapper. El alta inserta únicamente
// (dimension_id, code, name, description); la edición solo nombre y descripción, de modo
// que code queda físicamente inmutable. La lectura para edición usa SELECT FOR UPDATE
// dentro de la transacción del caso de uso: el bloqueo se emite sobre la fila completa y se
// mantiene hasta el commit, por lo que la actualización posterior observa exactamente el
// estado que el caso de uso acaba de leer. La traducción de SQLSTATE queda acotada a
// uq_dimension_code (23505): cualquier otra violación —incluidas las de otras restricciones
// de la tabla— se propaga sin traducir y termina en 500.
public sealed class DimensionAdministrationRepository(IDbConnectionFactory connectionFactory)
    : IDimensionAdministrationRepository
{
    private const string DuplicateCodeConstraintName = "uq_dimension_code";

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
            // Carrera entre el pre-check del caso de uso y esta inserción: se traduce con el
            // mismo mensaje del pre-check para que el 409 sea uniforme e indistinguible.
            throw new ConflictException(IDimensionAdministrationRepository.DuplicateCodeConflictMessage);
        }
    }

    public async Task<Dimension?> GetByIdForUpdateAsync(
        Guid dimensionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        // FOR UPDATE sobre la fila de la Dimensión: el bloqueo serializa la edición contra
        // otros PATCH concurrentes sobre la misma fila (última escritura gana) y se retiene
        // hasta el fin de la transacción del caso de uso.
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
        // Solo nombre y descripción: code no se incluye en el SET porque es inmutable tras
        // el alta (B5a). Tras el FOR UPDATE la fila no puede haber desaparecido, de modo que
        // no se necesita comprobación de filas afectadas.
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

    private sealed class DimensionRow
    {
        public Guid Dimension_id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }
}
