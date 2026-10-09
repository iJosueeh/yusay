namespace Yusay.Application.Tracking.Commands.CreateCheckIn;

public sealed record CreateCheckInCommand(
    DateTimeOffset? RecordedAt,
    string? Note,
    IReadOnlyList<CreateMeasurementInput> Measurements);
