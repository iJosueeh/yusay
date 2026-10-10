namespace Yusay.Api.Contracts.Tracking;

/// <summary>
/// DELETE de un CheckIn propio: transporta la revisión esperada en el cuerpo de la
/// petición (contrato simétrico con PUT) para la concurrencia optimista. La eliminación
/// no admite ventana temporal (OQ-DOM-009).
/// </summary>
public sealed record DeleteCheckInRequest(int? Revision);
