namespace Yusay.Api.Contracts.Administration;

public sealed record UpdateDimensionVersionRequest(
    string? Definition = null,
    int? MinValue = null,
    int? MaxValue = null,
    int? Step = null,
    IReadOnlyList<DimensionAnchorRequest>? Anchors = null);
