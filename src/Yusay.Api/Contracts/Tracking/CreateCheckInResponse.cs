namespace Yusay.Api.Contracts.Tracking;

public sealed record CreateCheckInResponse(
    Guid CheckInId,
    DateTimeOffset RecordedAt,
    DateTimeOffset CreatedAt,
    int Revision);
