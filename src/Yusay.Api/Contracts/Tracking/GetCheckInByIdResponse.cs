namespace Yusay.Api.Contracts.Tracking;

public sealed record GetCheckInByIdResponse(
    Guid CheckInId,
    DateTimeOffset RecordedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    int Revision,
    string? Note,
    IReadOnlyList<GetCheckInMeasurementResponse> Measurements);
