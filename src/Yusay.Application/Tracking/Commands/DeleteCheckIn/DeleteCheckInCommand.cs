namespace Yusay.Application.Tracking.Commands.DeleteCheckIn;

public sealed record DeleteCheckInCommand(
    Guid CheckInId,
    int? Revision);
