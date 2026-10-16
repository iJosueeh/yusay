namespace Yusay.Api.Contracts.Tracking;

public sealed record DimensionResponse(
    Guid DimensionId,
    string Code,
    string Name,
    string Description,
    IReadOnlyList<DimensionVersionResponse> Versions);
