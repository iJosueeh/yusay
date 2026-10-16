using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.UnitTests.Tracking;

public class DimensionTests
{
    private readonly DimensionCode _validCode = DimensionCode.Create("mood");

    [Fact]
    public void Create_WithValidParameters_ShouldInstantiateDimensionWithNewId()
    {
        var dimension = Dimension.Create(
            _validCode,
            "Estado de ánimo",
            "Evaluación de la percepción subjetiva del estado de ánimo.");

        Assert.NotEqual(Guid.Empty, dimension.DimensionId);
        Assert.Equal(_validCode, dimension.Code);
        Assert.Equal("Estado de ánimo", dimension.Name);
        Assert.Equal("Evaluación de la percepción subjetiva del estado de ánimo.", dimension.Description);
    }

    [Fact]
    public void Create_WithExplicitId_ShouldPreserveId()
    {
        var explicitId = Guid.NewGuid();
        var dimension = Dimension.Create(
            _validCode,
            "Estrés",
            "Nivel de estrés percibido.",
            dimensionId: explicitId);

        Assert.Equal(explicitId, dimension.DimensionId);
    }

    [Fact]
    public void Create_WithEmptyGuid_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Dimension.Create(_validCode, "Estrés", "Descripción.", dimensionId: Guid.Empty));

        Assert.Contains("UUID vacío", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldThrowDomainException(string? invalidName)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Dimension.Create(_validCode, invalidName!, "Descripción."));

        Assert.Contains("nombre de la dimensión no puede estar vacío", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ShouldThrowDomainException(string? invalidDescription)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Dimension.Create(_validCode, "Nombre", invalidDescription!));

        Assert.Contains("descripción de la dimensión no puede estar vacía", ex.Message);
    }

    [Fact]
    public void Create_WithPaddedStrings_ShouldTrimNameAndDescription()
    {
        var dimension = Dimension.Create(
            _validCode,
            "  Energía  ",
            "  Nivel percibido de energía diaria.  ");

        Assert.Equal("Energía", dimension.Name);
        Assert.Equal("Nivel percibido de energía diaria.", dimension.Description);
    }

    [Fact]
    public void Rehydrate_WithValidParameters_ShouldReconstituteDimension()
    {
        var id = Guid.NewGuid();
        var dimension = Dimension.Rehydrate(
            id,
            _validCode,
            "Sueño",
            "Calidad subjetiva del último periodo de sueño.");

        Assert.Equal(id, dimension.DimensionId);
        Assert.Equal(_validCode, dimension.Code);
        Assert.Equal("Sueño", dimension.Name);
        Assert.Equal("Calidad subjetiva del último periodo de sueño.", dimension.Description);
    }

    [Fact]
    public void Rehydrate_WithEmptyGuid_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Dimension.Rehydrate(Guid.Empty, _validCode, "Sueño", "Descripción."));

        Assert.Contains("UUID vacío", ex.Message);
    }

    [Fact]
    public void UpdateDetails_WithValidValues_ShouldUpdateNameAndDescription()
    {
        var dimension = Dimension.Create(_validCode, "Nombre Original", "Descripción Original.");

        dimension.UpdateDetails("Nombre Modificado", "Descripción Modificada.");

        Assert.Equal("Nombre Modificado", dimension.Name);
        Assert.Equal("Descripción Modificada.", dimension.Description);
    }

    [Theory]
    [InlineData("", "Descripción válida.")]
    [InlineData("   ", "Descripción válida.")]
    [InlineData("Nombre válido", "")]
    [InlineData("Nombre válido", "   ")]
    public void UpdateDetails_WithInvalidValues_ShouldThrowDomainException(string newName, string newDescription)
    {
        var dimension = Dimension.Create(_validCode, "Nombre Original", "Descripción Original.");

        Assert.Throws<DomainException>(() => dimension.UpdateDetails(newName, newDescription));
    }
}
