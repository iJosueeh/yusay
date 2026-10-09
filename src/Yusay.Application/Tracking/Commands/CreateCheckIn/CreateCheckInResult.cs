namespace Yusay.Application.Tracking.Commands.CreateCheckIn;

public sealed record CreateCheckInResult(
    Guid CheckInId,
    DateTimeOffset RecordedAt,
    DateTimeOffset CreatedAt,
    int Revision);
