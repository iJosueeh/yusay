using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.UnitTests.Tracking;

public class DimensionVersionTests
{
    private readonly Guid _dimensionId = Guid.NewGuid();
    private readonly DimensionScale _standardScale = DimensionScale.Create(1, 5, 1);
    private const string StandardDefinition = "Definición operativa de prueba para escala ordinal 1 a 5.";

    [Fact]
    public void CreateDraft_WithValidParameters_ShouldInstantiateInDraftStatusWithEmptyAnchors()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);

        Assert.NotEqual(Guid.Empty, version.DimensionVersionId);
        Assert.Equal(_dimensionId, version.DimensionId);
        Assert.Equal(1, version.Version);
        Assert.Equal(_standardScale, version.Scale);
        Assert.Equal(StandardDefinition, version.Definition);
        Assert.Equal(DimensionVersionStatus.Draft, version.Status);
        Assert.Empty(version.Anchors);
    }

    [Fact]
    public void CreateDraft_WithInitialAnchors_ShouldOrderAnchorsByValue()
    {
        var initial = new[]
        {
            DimensionAnchor.Create(5, "Muy positivo"),
            DimensionAnchor.Create(1, "Muy negativo"),
            DimensionAnchor.Create(3, "Neutral")
        };

        var version = DimensionVersion.CreateDraft(
            _dimensionId, 1, _standardScale, StandardDefinition, initialAnchors: initial);

        Assert.Equal(3, version.Anchors.Count);
        Assert.Equal(1, version.Anchors[0].Value);
        Assert.Equal(3, version.Anchors[1].Value);
        Assert.Equal(5, version.Anchors[2].Value);
    }

    [Fact]
    public void CreateDraft_WithInitialAnchorOutOfScale_ShouldThrowDomainException()
    {
        var initial = new[]
        {
            DimensionAnchor.Create(1, "Bajo"),
            DimensionAnchor.Create(6, "Fuera de escala") // 6 > max (5)
        };

        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition, initialAnchors: initial));

        Assert.Contains("no pertenece a la escala", ex.Message);
    }

    [Fact]
    public void CreateDraft_WithDuplicateInitialAnchorValues_ShouldThrowDomainException()
    {
        var initial = new[]
        {
            DimensionAnchor.Create(3, "Neutral 1"),
            DimensionAnchor.Create(3, "Neutral 2")
        };

        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition, initialAnchors: initial));

        Assert.Contains("duplicados para el valor 3", ex.Message);
    }

    [Fact]
    public void CreateDraft_WithEmptyDimensionId_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.CreateDraft(Guid.Empty, 1, _standardScale, StandardDefinition));

        Assert.Contains("Dimensión válida", ex.Message);
    }

    [Fact]
    public void CreateDraft_WithEmptyVersionId_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition, dimensionVersionId: Guid.Empty));

        Assert.Contains("UUID vacío", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void CreateDraft_WithNonPositiveVersion_ShouldThrowDomainException(int invalidVersion)
    {
        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.CreateDraft(_dimensionId, invalidVersion, _standardScale, StandardDefinition));

        Assert.Contains("entero positivo mayor a cero", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_WithEmptyDefinition_ShouldThrowDomainException(string? invalidDefinition)
    {
        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, invalidDefinition!));

        Assert.Contains("definición de la versión no puede estar vacía", ex.Message);
    }

    [Fact]
    public void Rehydrate_WithValidParameters_ShouldRestoreVersionFaithfully()
    {
        var versionId = Guid.NewGuid();
        var anchors = new[]
        {
            DimensionAnchor.Create(1, "Uno"),
            DimensionAnchor.Create(2, "Dos")
        };

        var version = DimensionVersion.Rehydrate(
            versionId,
            _dimensionId,
            2,
            _standardScale,
            StandardDefinition,
            DimensionVersionStatus.Active,
            anchors);

        Assert.Equal(versionId, version.DimensionVersionId);
        Assert.Equal(_dimensionId, version.DimensionId);
        Assert.Equal(2, version.Version);
        Assert.Equal(DimensionVersionStatus.Active, version.Status);
        Assert.Equal(2, version.Anchors.Count);
    }

    [Fact]
    public void Rehydrate_WithStructuralDuplicateAnchors_ShouldThrowDomainException()
    {
        var versionId = Guid.NewGuid();
        var duplicateAnchors = new[]
        {
            DimensionAnchor.Create(1, "Uno"),
            DimensionAnchor.Create(1, "Uno Duplicado")
        };

        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.Rehydrate(
                versionId,
                _dimensionId,
                1,
                _standardScale,
                StandardDefinition,
                DimensionVersionStatus.Active,
                duplicateAnchors));

        Assert.Contains("Estructura de anclajes corrupta", ex.Message);
    }

    [Fact]
    public void Rehydrate_WithUndefinedStatus_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            DimensionVersion.Rehydrate(
                Guid.NewGuid(),
                _dimensionId,
                1,
                _standardScale,
                StandardDefinition,
                (DimensionVersionStatus)999,
                null));

        Assert.Contains("no es un estado válido", ex.Message);
    }

    [Fact]
    public void AddOrUpdateAnchor_InDraft_WhenWithinScale_ShouldAddAndOverwriteCorrectly()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);

        version.AddOrUpdateAnchor(1, "Inicial");
        Assert.Single(version.Anchors);
        Assert.Equal("Inicial", version.Anchors[0].Label);

        // Actualizar ancla existente para el mismo valor
        version.AddOrUpdateAnchor(1, "Actualizado");
        Assert.Single(version.Anchors);
        Assert.Equal("Actualizado", version.Anchors[0].Label);

        // Agregar otra ancla
        version.AddOrUpdateAnchor(5, "Extremo superior");
        Assert.Equal(2, version.Anchors.Count);
        Assert.Equal(1, version.Anchors[0].Value);
        Assert.Equal(5, version.Anchors[1].Value);
    }

    [Fact]
    public void AddOrUpdateAnchor_InDraft_WhenOutOfScale_ShouldThrowDomainException()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);

        var ex = Assert.Throws<DomainException>(() => version.AddOrUpdateAnchor(6, "Fuera de escala"));
        Assert.Contains("no pertenece a la escala", ex.Message);
    }

    [Fact]
    public void AddOrUpdateAnchor_InDraft_WhenMisalignedToStep_ShouldThrowDomainException()
    {
        var steppedScale = DimensionScale.Create(1, 5, 2); // 1, 3, 5
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, steppedScale, StandardDefinition);

        var ex = Assert.Throws<DomainException>(() => version.AddOrUpdateAnchor(2, "Desalineado con step 2"));
        Assert.Contains("no pertenece a la escala", ex.Message);
    }

    [Fact]
    public void RemoveAnchor_InDraft_ShouldRemoveAnchor()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.AddOrUpdateAnchor(1, "Uno");
        version.AddOrUpdateAnchor(2, "Dos");

        version.RemoveAnchor(1);

        Assert.Single(version.Anchors);
        Assert.Equal(2, version.Anchors[0].Value);
    }

    [Fact]
    public void UpdateDefinition_InDraft_ShouldUpdateDefinition()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);

        version.UpdateDefinition("  Nueva definición operativa.  ");

        Assert.Equal("Nueva definición operativa.", version.Definition);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDefinition_InDraft_WithEmptyDefinition_ShouldThrowDomainException(string? invalidDef)
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);

        Assert.Throws<DomainException>(() => version.UpdateDefinition(invalidDef!));
    }

    [Fact]
    public void UpdateScale_InDraft_WhenCompatibleWithExistingAnchors_ShouldUpdateScale()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.AddOrUpdateAnchor(1, "Uno");
        version.AddOrUpdateAnchor(3, "Tres");

        var newCompatibleScale = DimensionScale.Create(1, 7, 2); // 1, 3, 5, 7
        version.UpdateScale(newCompatibleScale);

        Assert.Equal(newCompatibleScale, version.Scale);
    }

    [Fact]
    public void UpdateScale_InDraft_WhenIncompatibleWithExistingAnchors_ShouldThrowDomainException()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.AddOrUpdateAnchor(5, "Cinco");

        // Nueva escala 1..3 excluye el ancla en 5
        var narrowerScale = DimensionScale.Create(1, 3, 1);

        var ex = Assert.Throws<DomainException>(() => version.UpdateScale(narrowerScale));
        Assert.Contains("dejaría el anclaje existente con valor 5 fuera de la nueva escala", ex.Message);
    }

    [Fact]
    public void Activate_FromDraft_WithValidAnchors_ShouldTransitionToActive()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.AddOrUpdateAnchor(1, "Uno");
        version.AddOrUpdateAnchor(5, "Cinco");

        version.Activate();

        Assert.Equal(DimensionVersionStatus.Active, version.Status);
    }

    [Fact]
    public void Activate_FromDraft_WithZeroAnchors_ShouldSucceed()
    {
        // Cardinalidad 0..N: activación con cero anclas permitida según D-B2-2
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);

        version.Activate();

        Assert.Equal(DimensionVersionStatus.Active, version.Status);
        Assert.Empty(version.Anchors);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ShouldThrowDomainException()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.Activate();

        var ex = Assert.Throws<DomainException>(() => version.Activate());
        Assert.Contains("Solo DRAFT puede pasar a ACTIVE", ex.Message);
    }

    [Fact]
    public void Retire_FromActive_ShouldTransitionToRetired()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.Activate();

        version.Retire();

        Assert.Equal(DimensionVersionStatus.Retired, version.Status);
    }

    [Fact]
    public void Retire_FromDraft_ShouldThrowDomainException()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);

        var ex = Assert.Throws<DomainException>(() => version.Retire());
        Assert.Contains("Solo ACTIVE puede pasar a RETIRED", ex.Message);
    }

    [Fact]
    public void Retire_WhenAlreadyRetired_ShouldThrowDomainException()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.Activate();
        version.Retire();

        var ex = Assert.Throws<DomainException>(() => version.Retire());
        Assert.Contains("Solo ACTIVE puede pasar a RETIRED", ex.Message);
    }

    [Fact]
    public void Mutations_InActiveStatus_ShouldThrowDomainException()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.AddOrUpdateAnchor(1, "Uno");
        version.Activate();

        var newScale = DimensionScale.Create(1, 10, 1);
        Assert.Throws<DomainException>(() => version.UpdateScale(newScale));
        Assert.Throws<DomainException>(() => version.UpdateDefinition("Nueva def"));
        Assert.Throws<DomainException>(() => version.AddOrUpdateAnchor(2, "Dos"));
        Assert.Throws<DomainException>(() => version.RemoveAnchor(1));
    }

    [Fact]
    public void Mutations_InRetiredStatus_ShouldThrowDomainException()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.Activate();
        version.Retire();

        var newScale = DimensionScale.Create(1, 10, 1);
        Assert.Throws<DomainException>(() => version.UpdateScale(newScale));
        Assert.Throws<DomainException>(() => version.UpdateDefinition("Nueva def"));
        Assert.Throws<DomainException>(() => version.AddOrUpdateAnchor(2, "Dos"));
        Assert.Throws<DomainException>(() => version.RemoveAnchor(1));
        Assert.Throws<DomainException>(() => version.Activate());
    }

    [Fact]
    public void Anchors_ExposedAsIReadOnlyList_ShouldPreventExternalCollectionMutation()
    {
        var version = DimensionVersion.CreateDraft(_dimensionId, 1, _standardScale, StandardDefinition);
        version.AddOrUpdateAnchor(1, "Uno");

        var exposed = version.Anchors;

        // Intentar mutar externamente el array devuelto no debe afectar a la versión
        if (exposed is DimensionAnchor[] array)
        {
            array[0] = DimensionAnchor.Create(1, "Mutado");
        }

        Assert.Equal("Uno", version.Anchors[0].Label);
    }
}
