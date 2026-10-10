using System.Data.Common;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Infrastructure.Tracking.Repositories;

public sealed class DimensionVersionRepository(IDbConnectionFactory connectionFactory) : IDimensionVersionRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<ActiveDimensionScale?> GetActiveScaleAsync(
        Guid dimensionId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                dimension_version_id,
                min_value,
                max_value,
                step
            FROM yusay.dimension_version
            WHERE dimension_id = @DimensionId AND status = 'ACTIVE';
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var row = await conn.QuerySingleOrDefaultAsync<ScaleRow>(new CommandDefinition(
                sql, new { DimensionId = dimensionId }, transaction, cancellationToken: cancellationToken));

            return row is null
                ? null
                : new ActiveDimensionScale(row.Dimension_version_id, row.Min_value, row.Max_value, row.Step);
        }, cancellationToken);
    }

    public async Task<StoredDimensionScale?> GetScaleByVersionIdAsync(
        Guid dimensionVersionId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        // Sin filtro de estado: la edición se valida contra la versión almacenada aunque
        // esté RETIRED (OQ-DOM-008); V015 mantiene min/max/step congelados.
        const string sql = """
            SELECT
                dimension_version_id,
                min_value,
                max_value,
                step
            FROM yusay.dimension_version
            WHERE dimension_version_id = @DimensionVersionId;
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var row = await conn.QuerySingleOrDefaultAsync<ScaleRow>(new CommandDefinition(
                sql, new { DimensionVersionId = dimensionVersionId }, transaction, cancellationToken: cancellationToken));

            return row is null
                ? null
                : new StoredDimensionScale(row.Dimension_version_id, row.Min_value, row.Max_value, row.Step);
        }, cancellationToken);
    }

    public async Task<bool> DimensionExistsAsync(
        Guid dimensionId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1
                FROM yusay.dimension
                WHERE dimension_id = @DimensionId
            );
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(
                sql, new { DimensionId = dimensionId }, transaction, cancellationToken: cancellationToken);
            return await conn.ExecuteScalarAsync<bool>(command);
        }, cancellationToken);
    }

    private async Task<T> ExecuteWithConnectionAsync<T>(
        DbTransaction? transaction,
        Func<DbConnection, Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (transaction?.Connection != null)
        {
            return await action(transaction.Connection);
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await action(connection);
    }

    private sealed class ScaleRow
    {
        public Guid Dimension_version_id { get; init; }
        public int Min_value { get; init; }
        public int Max_value { get; init; }
        public int Step { get; init; }
    }
}
