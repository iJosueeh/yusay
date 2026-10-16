using Yusay.Domain.Tracking.Enums;

namespace Yusay.Application.Tracking.Queries.Dimensions;

public sealed record DimensionCatalogResult(
    Guid DimensionId,
    string Code,
    string Name,
    string Description,
    IReadOnlyList<DimensionVersionResult> Versions);

public sealed record DimensionVersionResult(
    Guid DimensionVersionId,
    int Version,
    DimensionVersionStatus Status,
    string Definition,
    int MinValue,
    int MaxValue,
    int Step,
    IReadOnlyList<DimensionAnchorResult> Anchors);

public sealed record DimensionAnchorResult(int Value, string Label);
