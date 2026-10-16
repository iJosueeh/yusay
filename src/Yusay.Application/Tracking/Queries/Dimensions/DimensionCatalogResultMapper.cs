using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Queries.Dimensions;

internal static class DimensionCatalogResultMapper
{
    public static IReadOnlyList<DimensionCatalogResult> ToResults(
        IEnumerable<DimensionWithVersions> entries) =>
        entries.Select(ToResult).ToArray();

    public static DimensionCatalogResult ToResult(DimensionWithVersions entry) => new(
        entry.Dimension.DimensionId,
        entry.Dimension.Code,
        entry.Dimension.Name,
        entry.Dimension.Description,
        entry.Versions.Select(ToResult).ToArray());

    private static DimensionVersionResult ToResult(DimensionVersion version) => new(
        version.DimensionVersionId,
        version.Version,
        version.Status,
        version.Definition,
        version.Scale.MinValue,
        version.Scale.MaxValue,
        version.Scale.Step,
        version.Anchors
            .Select(anchor => new DimensionAnchorResult(anchor.Value, anchor.Label))
            .ToArray());
}
