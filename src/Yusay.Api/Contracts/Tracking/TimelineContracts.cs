namespace Yusay.Api.Contracts.Tracking;

public sealed record TimelineResponse(
    IReadOnlyList<TimelineItemResponse> Items,
    string? NextCursor);

public sealed record TimelineItemResponse(
    Guid EventId,
    string Type,
    DateTimeOffset OccurredAt,
    TimelineAssessmentResponse? Assessment,
    TimelineCheckInResponse? CheckIn);

public sealed record TimelineAssessmentResponse(
    Guid AttemptId,
    Guid InstrumentId,
    string InstrumentCode,
    string InstrumentName,
    Guid InstrumentVersionId,
    int Version,
    int Score,
    TimelineInterpretationResponse Interpretation);

public sealed record TimelineInterpretationResponse(
    Guid InterpretationId,
    string Label);

public sealed record TimelineCheckInResponse(
    Guid CheckInId,
    string? Note,
    IReadOnlyList<TimelineMeasurementResponse> Measurements);

public sealed record TimelineMeasurementResponse(
    Guid DimensionId,
    string DimensionCode,
    string DimensionName,
    Guid DimensionVersionId,
    int Value);
