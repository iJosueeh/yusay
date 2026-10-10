namespace Yusay.Application.Tracking.Commands.DeleteCheckIn;

/// <summary>
/// DELETE de un CheckIn propio: borrado físico definitivo bajo concurrencia optimista.
/// La revisión esperada es obligatoria y viaja en el cuerpo de la petición (contrato
/// simétrico con PUT); la eliminación está permitida en cualquier momento, sin ventana
/// temporal (OQ-DOM-009 / RN-022).
/// </summary>
public sealed record DeleteCheckInCommand(
    Guid CheckInId,
    int? Revision);
