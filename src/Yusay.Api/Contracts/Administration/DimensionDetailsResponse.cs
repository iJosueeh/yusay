namespace Yusay.Api.Contracts.Administration;

// Representación fina de Dimension para la escritura administrativa (B5a de RF-021): sin
// colección de versiones, que B5b añadirá en sus endpoints correspondientes.
public sealed record DimensionDetailsResponse(
    Guid DimensionId,
    string Code,
    string Name,
    string Description);
