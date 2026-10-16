namespace Yusay.Api.Contracts.Administration;

// B5a de RF-021: alta administrativa de Dimension. La validación de forma de code, name y
// description la resuelve el dominio (400); el duplicado de code responde 409.
public sealed record CreateDimensionRequest(
    string Code,
    string Name,
    string Description);
