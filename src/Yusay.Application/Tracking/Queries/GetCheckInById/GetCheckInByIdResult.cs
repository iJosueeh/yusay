namespace Yusay.Application.Tracking.Queries.GetCheckInById;

public sealed record GetCheckInByIdResult(
    Guid CheckInId,
    DateTimeOffset RecordedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Revision,
    string? Note,
    IReadOnlyList<GetCheckInMeasurementResult> Measurements);

public sealed record GetCheckInMeasurementResult(
    Guid DimensionId,
    Guid DimensionVersionId,
    int Value);
