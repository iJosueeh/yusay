namespace Yusay.Application.Tracking.Commands.UpdateDimension;

// Estado final de la Dimensión tras la edición efectiva, con el code inmutable incluido.
public sealed record UpdateDimensionResult(
    Guid DimensionId,
    string Code,
    string Name,
    string Description);
