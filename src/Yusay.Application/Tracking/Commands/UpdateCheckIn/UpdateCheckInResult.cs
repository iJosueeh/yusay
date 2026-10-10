namespace Yusay.Application.Tracking.Commands.UpdateCheckIn;

public sealed record UpdateCheckInResult(
    Guid CheckInId,
    DateTimeOffset RecordedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Revision,
    string? Note,
    IReadOnlyList<UpdateCheckInMeasurementResult> Measurements);

public sealed record UpdateCheckInMeasurementResult(
    Guid DimensionId,
    Guid DimensionVersionId,
    int Value);
