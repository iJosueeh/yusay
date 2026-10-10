namespace Yusay.Application.Tracking.Commands.UpdateCheckIn;

public sealed record UpdateCheckInCommand(
    Guid CheckInId,
    int? Revision,
    DateTimeOffset? RecordedAt,
    string? Note,
    IReadOnlyList<UpdateMeasurementInput>? Measurements);
