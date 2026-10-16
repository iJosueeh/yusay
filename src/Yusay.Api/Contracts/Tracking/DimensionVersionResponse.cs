namespace Yusay.Api.Contracts.Tracking;

public sealed record DimensionVersionResponse(
    Guid DimensionVersionId,
    int Version,
    string Status,
    string Definition,
    int MinValue,
    int MaxValue,
    int Step,
    IReadOnlyList<DimensionAnchorResponse> Anchors);
