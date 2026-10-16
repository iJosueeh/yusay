namespace Yusay.Application.Tracking.Commands.UpdateDimension;

// Semántica PATCH (B5a de RF-021): un campo ausente o null conserva el valor almacenado;
// code viaja fuera del contrato porque es inmutable tras el alta.
public sealed record UpdateDimensionCommand(
    Guid DimensionId,
    string? Name,
    string? Description);
