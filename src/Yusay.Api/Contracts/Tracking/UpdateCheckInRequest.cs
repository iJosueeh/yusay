namespace Yusay.Api.Contracts.Tracking;

public sealed record UpdateCheckInRequest(
    int? Revision,
    DateTimeOffset? RecordedAt,
    string? Note,
    IReadOnlyList<UpdateCheckInMeasurementRequest>? Measurements);
