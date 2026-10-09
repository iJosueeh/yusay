namespace Yusay.Api.Contracts.Tracking;

public sealed record CreateCheckInRequest(
    DateTimeOffset? RecordedAt,
    string? Note,
    IReadOnlyList<CreateCheckInMeasurementRequest>? Measurements);
