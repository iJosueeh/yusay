using System.Text.Json;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.GetTimeline;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Infrastructure.Tracking.Repositories;

public sealed class TimelineRepository(IDbConnectionFactory connectionFactory) : ITimelineRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<IReadOnlyList<TimelineItemDto>> GetTimelineAsync(
        Guid userId,
        int limit,
        DateTimeOffset? cursorOccurredAt,
        string? cursorEventType,
        Guid? cursorEventId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? type,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            WITH assessment_events AS (
                SELECT
                    a.attempt_id AS event_id,
                    'assessment' AS event_type,
                    a.ended_at AS occurred_at,
                    a.instrument_id,
                    i.code AS instrument_code,
                    i.name AS instrument_name,
                    a.instrument_version_id,
                    v.version AS instrument_version,
                    r.score,
                    it.interpretation_id,
                    it.label AS interpretation_label,
                    NULL::text AS note,
                    '[]'::json AS measurements_json
                FROM yusay.assessment_attempt a
                JOIN yusay.assessment_result r ON r.attempt_id = a.attempt_id
                JOIN yusay.instrument i ON i.instrument_id = a.instrument_id
                JOIN yusay.instrument_version v ON v.instrument_version_id = a.instrument_version_id
                JOIN yusay.interpretation it ON it.interpretation_id = r.interpretation_id
                WHERE a.user_id = @UserId
                  AND a.status = 'SUBMITTED'
                  AND (@FilterType IS NULL OR @FilterType = 'assessment')
                  AND (@From::timestamptz IS NULL OR a.ended_at >= @From::timestamptz)
                  AND (@To::timestamptz IS NULL OR a.ended_at <= @To::timestamptz)
            ),
            check_in_events AS (
                SELECT
                    c.check_in_id AS event_id,
                    'check_in' AS event_type,
                    c.recorded_at AS occurred_at,
                    NULL::uuid AS instrument_id,
                    NULL::text AS instrument_code,
                    NULL::text AS instrument_name,
                    NULL::uuid AS instrument_version_id,
                    NULL::int AS instrument_version,
                    NULL::int AS score,
                    NULL::uuid AS interpretation_id,
                    NULL::text AS interpretation_label,
                    c.note,
                    COALESCE((
                        SELECT json_agg(json_build_object(
                            'dimensionId', m.dimension_id,
                            'dimensionCode', d.code,
                            'dimensionName', d.name,
                            'dimensionVersionId', m.dimension_version_id,
                            'value', m.value
                        ) ORDER BY d.code)
                        FROM yusay.measurement m
                        JOIN yusay.dimension d ON d.dimension_id = m.dimension_id
                        WHERE m.check_in_id = c.check_in_id
                    ), '[]'::json) AS measurements_json
                FROM yusay.check_in c
                WHERE c.user_id = @UserId
                  AND (@FilterType IS NULL OR @FilterType = 'check_in')
                  AND (@From::timestamptz IS NULL OR c.recorded_at >= @From::timestamptz)
                  AND (@To::timestamptz IS NULL OR c.recorded_at <= @To::timestamptz)
            ),
            all_events AS (
                SELECT * FROM assessment_events
                UNION ALL
                SELECT * FROM check_in_events
            )
            SELECT
                event_id AS EventId,
                event_type AS EventType,
                occurred_at AS OccurredAt,
                instrument_id AS InstrumentId,
                instrument_code AS InstrumentCode,
                instrument_name AS InstrumentName,
                instrument_version_id AS InstrumentVersionId,
                instrument_version AS InstrumentVersion,
                score AS Score,
                interpretation_id AS InterpretationId,
                interpretation_label AS InterpretationLabel,
                note AS Note,
                measurements_json::text AS MeasurementsJson
            FROM all_events
            WHERE (
                @HasCursor = false
                OR (
                    occurred_at < @CursorOccurredAt::timestamptz
                    OR (
                        occurred_at = @CursorOccurredAt::timestamptz
                        AND (
                            event_type < @CursorEventType
                            OR (event_type = @CursorEventType AND event_id < @CursorEventId::uuid)
                        )
                    )
                )
            )
            ORDER BY occurred_at DESC, event_type DESC, event_id DESC
            LIMIT @Limit;
            """;

        var hasCursor = cursorOccurredAt.HasValue
            && !string.IsNullOrEmpty(cursorEventType)
            && cursorEventId.HasValue;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await connection.QueryAsync<TimelineEventRow>(new CommandDefinition(
            sql,
            new
            {
                UserId = userId,
                HasCursor = hasCursor,
                CursorOccurredAt = cursorOccurredAt,
                CursorEventType = cursorEventType,
                CursorEventId = cursorEventId,
                From = from,
                To = to,
                FilterType = type,
                Limit = limit
            },
            cancellationToken: cancellationToken))).ToList();

        var result = new List<TimelineItemDto>(rows.Count);
        foreach (var row in rows)
        {
            if (row.EventType == "assessment")
            {
                var assessmentDto = new TimelineAssessmentDto(
                    row.EventId,
                    row.InstrumentId!.Value,
                    row.InstrumentCode!,
                    row.InstrumentName!,
                    row.InstrumentVersionId!.Value,
                    row.InstrumentVersion!.Value,
                    row.Score!.Value,
                    new TimelineInterpretationDto(
                        row.InterpretationId!.Value,
                        row.InterpretationLabel!));

                result.Add(new TimelineItemDto(
                    row.EventId,
                    "assessment",
                    row.OccurredAt,
                    assessmentDto,
                    null));
            }
            else if (row.EventType == "check_in")
            {
                var parsedMeasurements =
                    JsonSerializer.Deserialize<List<MeasurementJsonRow>>(row.MeasurementsJson, JsonOptions) ?? [];

                if (parsedMeasurements.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Violación de integridad RN-030: el CheckIn {row.EventId} no contiene Measurements.");
                }

                var measurementDtos = parsedMeasurements
                    .Select(m => new TimelineMeasurementDto(
                        m.DimensionId,
                        m.DimensionCode,
                        m.DimensionName,
                        m.DimensionVersionId,
                        m.Value))
                    .ToList();

                var checkInDto = new TimelineCheckInDto(
                    row.EventId,
                    row.Note,
                    measurementDtos);

                result.Add(new TimelineItemDto(
                    row.EventId,
                    "check_in",
                    row.OccurredAt,
                    null,
                    checkInDto));
            }
            else
            {
                throw new InvalidOperationException($"Tipo de evento desconocido: {row.EventType}");
            }
        }

        return result;
    }

    private sealed class TimelineEventRow
    {
        public Guid EventId { get; init; }
        public string EventType { get; init; } = string.Empty;
        public DateTimeOffset OccurredAt { get; init; }
        public Guid? InstrumentId { get; init; }
        public string? InstrumentCode { get; init; }
        public string? InstrumentName { get; init; }
        public Guid? InstrumentVersionId { get; init; }
        public int? InstrumentVersion { get; init; }
        public int? Score { get; init; }
        public Guid? InterpretationId { get; init; }
        public string? InterpretationLabel { get; init; }
        public string? Note { get; init; }
        public string MeasurementsJson { get; init; } = "[]";
    }

    private sealed record MeasurementJsonRow(
        Guid DimensionId,
        string DimensionCode,
        string DimensionName,
        Guid DimensionVersionId,
        int Value);
}
