using System.Text.Json;
using System.Data.Common;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Infrastructure.Tracking.Repositories;

public sealed class CheckInRepository(IDbConnectionFactory connectionFactory) : ICheckInRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

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

    public async Task<bool> TryUpdateOwnedAsync(
        Guid checkInId,
        Guid ownerId,
        int expectedRevision,
        DateTimeOffset recordedAt,
        string? note,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH instant AS MATERIALIZED (
                SELECT clock_timestamp() AS now_instant
            )
            UPDATE yusay.check_in
            SET revision = revision + 1,
                updated_at = instant.now_instant,
                recorded_at = @RecordedAt,
                note = @Note
            FROM instant
            WHERE check_in_id = @CheckInId
              AND user_id = @OwnerId
              AND revision = @ExpectedRevision
              AND instant.now_instant < created_at + interval '168 hours';
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var affected = await conn.ExecuteAsync(new CommandDefinition(
                sql,
                new { CheckInId = checkInId, OwnerId = ownerId, ExpectedRevision = expectedRevision, RecordedAt = recordedAt, Note = note },
                transaction,
                cancellationToken: cancellationToken));
            return affected == 1;
        }, cancellationToken);
    }

    public async Task<int> TryUpdateOwnedMeasurementsAsync(
        Guid checkInId,
        IReadOnlyList<Measurement> measurements,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.measurement
            SET value = @Value
            WHERE check_in_id = @CheckInId AND dimension_id = @DimensionId;
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var affected = 0;
            foreach (var measurement in measurements)
            {
                affected += await conn.ExecuteAsync(new CommandDefinition(
                    sql,
                    new { CheckInId = checkInId, measurement.DimensionId, measurement.Value },
                    transaction,
                    cancellationToken: cancellationToken));
            }

            return affected;
        }, cancellationToken);
    }

    public async Task<bool> TryDeleteOwnedAsync(
        Guid checkInId,
        Guid ownerId,
        int expectedRevision,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        // OQ-DOM-009: eliminación permitida en cualquier momento (sin predicado temporal)
        // con revisión optimista. El único DELETE de la operación dispara las cascadas FK
        // de measurement y check_in_context_tag dentro de esta misma transacción.
        const string sql = """
            DELETE FROM yusay.check_in
            WHERE check_in_id = @CheckInId
              AND user_id = @OwnerId
              AND revision = @ExpectedRevision;
            """;

        return await ExecuteWithConnectionAsync(transaction, async conn =>
        {
            var affected = await conn.ExecuteAsync(new CommandDefinition(
                sql,
                new { CheckInId = checkInId, OwnerId = ownerId, ExpectedRevision = expectedRevision },
                transaction,
                cancellationToken: cancellationToken));
            return affected == 1;
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<CheckIn>> ListOwnedPageAsync(
        Guid ownerId,
        int fetchLimit,
        DateTimeOffset? cursorRecordedAt,
        Guid? cursorCheckInId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH page AS (
                SELECT
                    check_in_id,
                    recorded_at,
                    created_at,
                    updated_at,
                    revision,
                    note
                FROM yusay.check_in
                WHERE user_id = @OwnerId
                  AND (@HasCursor = false OR (recorded_at, check_in_id) < (@CursorRecordedAt, @CursorCheckInId))
                ORDER BY recorded_at DESC, check_in_id DESC
                LIMIT @FetchLimit
            )
            SELECT
                p.check_in_id,
                p.recorded_at,
                p.created_at,
                p.updated_at,
                p.revision,
                p.note,
                COALESCE((
                    SELECT json_agg(json_build_object(
                        'dimensionId', m.dimension_id,
                        'dimensionVersionId', m.dimension_version_id,
                        'value', m.value)
                    ORDER BY m.dimension_id)
                    FROM yusay.measurement m
                    WHERE m.check_in_id = p.check_in_id
                ), '[]'::json) AS measurements
            FROM page p
            ORDER BY p.recorded_at DESC, p.check_in_id DESC;
            """;

        return await ExecuteWithConnectionAsync(null, async conn =>
        {
            var rows = await conn.QueryAsync<CheckInListRow>(new CommandDefinition(
                sql,
                new
                {
                    OwnerId = ownerId,
                    FetchLimit = fetchLimit,
                    HasCursor = cursorRecordedAt is not null && cursorCheckInId is not null,
                    CursorRecordedAt = cursorRecordedAt,
                    CursorCheckInId = cursorCheckInId
                },
                cancellationToken: cancellationToken));

            return rows
                .Select(row =>
                {
                    var storedMeasurements =
                        JsonSerializer.Deserialize<ListMeasurementRow[]>(row.Measurements, JsonOptions) ?? [];
                    if (storedMeasurements.Length == 0)
                    {
                        throw new InvalidOperationException(
                            $"Violación de integridad RN-030: el CheckIn {row.Check_in_id} no contiene Measurements.");
                    }

                    return CheckIn.Rehydrate(
                        row.Check_in_id,
                        ownerId,
                        row.Recorded_at,
                        row.Created_at,
                        row.Updated_at,
                        row.Revision,
                        row.Note,
                        storedMeasurements
                            .Select(measurement => Measurement.Rehydrate(
                                measurement.DimensionId,
                                measurement.DimensionVersionId,
                                measurement.Value))
                            .ToArray());
                })
                .ToArray();
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

    private sealed class CheckInListRow
    {
        public Guid Check_in_id { get; init; }
        public DateTimeOffset Recorded_at { get; init; }
        public DateTimeOffset Created_at { get; init; }
        public DateTimeOffset? Updated_at { get; init; }
        public int Revision { get; init; }
        public string? Note { get; init; }
        public string Measurements { get; init; } = string.Empty;
    }

    private sealed record ListMeasurementRow(Guid DimensionId, Guid DimensionVersionId, int Value);
}
