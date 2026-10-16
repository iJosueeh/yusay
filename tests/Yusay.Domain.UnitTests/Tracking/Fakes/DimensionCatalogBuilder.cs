using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.UnitTests.Tracking.Fakes;

// Construcción de composiciones de catálogo para los tests unitarios de los casos de uso
// de B4: entidades de dominio rehidratadas, igual que las construye el repositorio real.
// Un array de estados vacío representa una Dimensión aún sin versiones.
public static class DimensionCatalogBuilder
{
    public static DimensionWithVersions Item(params DimensionVersionStatus[] statuses)
    {
        var dimension = Dimension.Create(
            DimensionCode.Create($"dim_{Guid.NewGuid():N}"),
            "Dimensión de prueba",
            "Descripción de prueba.");

        var versions = statuses
            .Select((status, index) => DimensionVersion.Rehydrate(
                Guid.NewGuid(),
                dimension.DimensionId,
                index + 1,
                DimensionScale.Create(1, 5, 1),
                "Definición de la versión.",
                status,
                [
                    DimensionAnchor.Create(1, "Ancla mínima"),
                    DimensionAnchor.Create(5, "Ancla máxima")
                ]))
            .ToArray();

        return new DimensionWithVersions(dimension, versions);
    }
}
