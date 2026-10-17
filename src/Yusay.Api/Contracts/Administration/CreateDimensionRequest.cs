namespace Yusay.Api.Contracts.Administration;

public sealed record CreateDimensionRequest(
    string Code,
    string Name,
    string Description);
