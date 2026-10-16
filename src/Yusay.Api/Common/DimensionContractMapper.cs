using Yusay.Api.Contracts.Tracking;
using Yusay.Application.Tracking.Queries.Dimensions;
using Yusay.Domain.Tracking.Enums;

namespace Yusay.Api.Common;

// Mapeo de los resultados de consulta de catálogo a los contratos HTTP (B4 de RF-021).
// Se centraliza aquí porque lo consumen dos controladores con políticas distintas.
internal static class DimensionContractMapper
{
    public static ListDimensionsResponse ToListResponse(IReadOnlyList<DimensionCatalogResult> items) =>
        new(items.Select(ToResponse).ToArray());

    public static DimensionResponse ToResponse(DimensionCatalogResult item) => new(
        item.DimensionId,
        item.Code,
        item.Name,
        item.Description,
        item.Versions.Select(ToResponse).ToArray());

    public static DimensionVersionResponse ToResponse(DimensionVersionResult version) => new(
        version.DimensionVersionId,
        version.Version,
        MapStatus(version.Status),
        version.Definition,
        version.MinValue,
        version.MaxValue,
        version.Step,
        version.Anchors
            .Select(anchor => new DimensionAnchorResponse(anchor.Value, anchor.Label))
            .ToArray());

    // Estado canónico de ck_dimension_version_status (V004): mayúsculas exactas, sin
    // serialización de enum global que altere el comportamiento de otros endpoints.
    private static string MapStatus(DimensionVersionStatus status) => status switch
    {
        DimensionVersionStatus.Draft => "DRAFT",
        DimensionVersionStatus.Active => "ACTIVE",
        DimensionVersionStatus.Retired => "RETIRED",
        _ => throw new ArgumentOutOfRangeException(
            nameof(status), status, "Estado de DimensionVersion no reconocido.")
    };
}
