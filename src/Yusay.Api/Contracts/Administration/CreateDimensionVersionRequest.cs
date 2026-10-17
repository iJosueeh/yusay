namespace Yusay.Api.Contracts.Administration;

public sealed record CreateDimensionVersionRequest(
    int Version,
    string Definition,
    int MinValue,
    int MaxValue,
    int Step,
    IReadOnlyList<DimensionAnchorRequest>? Anchors = null);
