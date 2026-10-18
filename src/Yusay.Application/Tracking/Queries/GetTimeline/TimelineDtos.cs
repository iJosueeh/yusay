namespace Yusay.Application.Tracking.Queries.GetTimeline;

public sealed record TimelineResult(
    IReadOnlyList<TimelineItemDto> Items,
    string? NextCursor);

public sealed record TimelineItemDto(
    Guid EventId,
    string Type,
    DateTimeOffset OccurredAt,
    TimelineAssessmentDto? Assessment,
    TimelineCheckInDto? CheckIn);

public sealed record TimelineAssessmentDto(
    Guid AttemptId,
    Guid InstrumentId,
    string InstrumentCode,
    string InstrumentName,
    Guid InstrumentVersionId,
    int Version,
    int Score,
    TimelineInterpretationDto Interpretation);

public sealed record TimelineInterpretationDto(
    Guid InterpretationId,
    string Label);

public sealed record TimelineCheckInDto(
    Guid CheckInId,
    string? Note,
    IReadOnlyList<TimelineMeasurementDto> Measurements);

public sealed record TimelineMeasurementDto(
    Guid DimensionId,
    string DimensionCode,
    string DimensionName,
    Guid DimensionVersionId,
    int Value);
