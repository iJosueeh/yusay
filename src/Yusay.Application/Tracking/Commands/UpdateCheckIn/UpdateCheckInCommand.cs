namespace Yusay.Application.Tracking.Commands.UpdateCheckIn;

/// <summary>
/// PUT de un CheckIn propio: sustituye el estado editable completo (recordedAt, note
/// y el valor de cada Measurement) bajo concurrencia optimista. La revisión esperada
/// es obligatoria; el conjunto de dimensiones y sus DimensionVersion son inmutables.
/// </summary>
public sealed record UpdateCheckInCommand(
    Guid CheckInId,
    int? Revision,
    DateTimeOffset? RecordedAt,
    string? Note,
    IReadOnlyList<UpdateMeasurementInput>? Measurements);
