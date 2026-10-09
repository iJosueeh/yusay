using System.Data.Common;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Infrastructure.Tracking.Repositories;

public sealed class CheckInRepository(IDbConnectionFactory connectionFactory) : ICheckInRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<DateTimeOffset> GetDatabaseTimestampAsync(
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT clock_timestamp();";

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var command = new CommandDefinition(sql, transaction: transaction, cancellationToken: cancellationToken);
            var value = await conn.ExecuteScalarAsync<DateTime>(command);
            return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
        }, cancellationToken);
    }

    public async Task CreateAsync(
        CheckIn checkIn,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string insertCheckInSql = """
            INSERT INTO yusay.check_in (
                check_in_id,
                user_id,
                recorded_at,
                created_at,
                revision,
                note
            ) VALUES (
                @CheckInId,
                @UserId,
                @RecordedAt,
                @CreatedAt,
                @Revision,
                @Note
            );
            """;

        const string insertMeasurementSql = """
            INSERT INTO yusay.measurement (
                check_in_id,
                dimension_id,
                dimension_version_id,
                value
            ) VALUES (
                @CheckInId,
                @DimensionId,
                @DimensionVersionId,
                @Value
            );
            """;

        var checkInParameters = new
        {
            checkIn.CheckInId,
            checkIn.UserId,
            checkIn.RecordedAt,
            checkIn.CreatedAt,
            checkIn.Revision,
            checkIn.Note
        };

        await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            await conn.ExecuteAsync(new CommandDefinition(
                insertCheckInSql, checkInParameters, transaction, cancellationToken: cancellationToken));

            foreach (var measurement in checkIn.Measurements)
            {
                var measurementParameters = new
                {
                    checkIn.CheckInId,
                    measurement.DimensionId,
                    measurement.DimensionVersionId,
                    measurement.Value
                };

                await conn.ExecuteAsync(new CommandDefinition(
                    insertMeasurementSql, measurementParameters, transaction, cancellationToken: cancellationToken));
            }
        }, cancellationToken);
    }

    public async Task<CheckIn?> GetByIdOwnedAsync(
        Guid checkInId,
        Guid ownerId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string selectCheckInSql = """
            SELECT
                check_in_id,
                user_id,
                recorded_at,
                created_at,
                updated_at,
                revision,
                note
            FROM yusay.check_in
            WHERE check_in_id = @CheckInId AND user_id = @OwnerId;
            """;

        const string selectMeasurementsSql = """
            SELECT
                dimension_id,
                dimension_version_id,
                value
            FROM yusay.measurement
            WHERE check_in_id = @CheckInId
            ORDER BY dimension_id;
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var row = await conn.QuerySingleOrDefaultAsync<CheckInRow>(new CommandDefinition(
                selectCheckInSql, new { CheckInId = checkInId, OwnerId = ownerId }, transaction,
                cancellationToken: cancellationToken));

            if (row is null)
            {
                return null;
            }

            var measurementRows = await conn.QueryAsync<MeasurementRow>(new CommandDefinition(
                selectMeasurementsSql, new { CheckInId = checkInId }, transaction,
                cancellationToken: cancellationToken));

            var measurements = measurementRows
                .Select(measurement => Measurement.Rehydrate(
                    measurement.Dimension_id,
                    measurement.Dimension_version_id,
                    measurement.Value))
                .ToArray();

            return CheckIn.Rehydrate(
                row.Check_in_id,
                row.User_id,
                row.Recorded_at,
                row.Created_at,
                row.Updated_at,
                row.Revision,
                row.Note,
                measurements);
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

    private async Task ExecuteWithConnectionAsync(
        DbTransaction? transaction,
        Func<DbConnection, Task> action,
        CancellationToken cancellationToken)
    {
        if (transaction?.Connection != null)
        {
            await action(transaction.Connection);
            return;
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await action(connection);
    }

    private sealed class CheckInRow
    {
        public Guid Check_in_id { get; init; }
        public Guid User_id { get; init; }
        public DateTimeOffset Recorded_at { get; init; }
        public DateTimeOffset Created_at { get; init; }
        public DateTimeOffset? Updated_at { get; init; }
        public int Revision { get; init; }
        public string? Note { get; init; }
    }

    private sealed class MeasurementRow
    {
        public Guid Dimension_id { get; init; }
        public Guid Dimension_version_id { get; init; }
        public int Value { get; init; }
    }
}
