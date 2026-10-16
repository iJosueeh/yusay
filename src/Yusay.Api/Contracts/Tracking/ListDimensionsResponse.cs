namespace Yusay.Api.Contracts.Tracking;

public sealed record ListDimensionsResponse(
    IReadOnlyList<DimensionResponse> Items);
