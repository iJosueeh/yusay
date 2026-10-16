namespace Yusay.Application.Tracking.Commands.CreateDimension;

// Representación fina de la Dimensión creada: sin colección de versiones (una Dimensión
// recién creada no tiene ninguna hasta B5b).
public sealed record CreateDimensionResult(
    Guid DimensionId,
    string Code,
    string Name,
    string Description);
