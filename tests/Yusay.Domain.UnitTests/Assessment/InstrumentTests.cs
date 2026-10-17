using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.ValueObjects;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Assessment;

public class InstrumentTests
{
    private readonly InstrumentCode _validCode = InstrumentCode.Create("phq-9");

    [Fact]
    public void Create_WithValidParameters_ShouldInstantiate()
    {
        var inst = Instrument.Create(
            _validCode,
            "Patient Health Questionnaire-9",
            "Instrumento para despistaje de depresión.",
            "Diagnóstico y seguimiento clínico.");

        Assert.NotEqual(Guid.Empty, inst.InstrumentId);
        Assert.Equal(_validCode, inst.Code);
        Assert.Equal("Patient Health Questionnaire-9", inst.Name);
        Assert.Equal("Instrumento para despistaje de depresión.", inst.Description);
        Assert.Equal("Diagnóstico y seguimiento clínico.", inst.Purpose);
    }

    [Fact]
    public void Create_WithExplicitId_ShouldPreserveId()
    {
        var id = Guid.NewGuid();
        var inst = Instrument.Create(
            _validCode,
            "PHQ-9",
            "Descripción.",
            "Propósito.",
            instrumentId: id);

        Assert.Equal(id, inst.InstrumentId);
    }

    [Fact]
    public void Create_WithEmptyGuid_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            Instrument.Create(_validCode, "PHQ-9", "Descripción.", "Propósito.", instrumentId: Guid.Empty));

        Assert.Contains("UUID vacío", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidName_ShouldThrowDomainException(string? invalidName)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Instrument.Create(_validCode, invalidName!, "Descripción.", "Propósito."));

        Assert.Contains("nombre del instrumento no puede estar vacío", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ShouldThrowDomainException(string? invalidDesc)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Instrument.Create(_validCode, "PHQ-9", invalidDesc!, "Propósito."));

        Assert.Contains("descripción del instrumento no puede estar vacía", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidPurpose_ShouldThrowDomainException(string? invalidPurpose)
    {
        var ex = Assert.Throws<DomainException>(() =>
            Instrument.Create(_validCode, "PHQ-9", "Descripción.", invalidPurpose!));

        Assert.Contains("propósito del instrumento no puede estar vacío", ex.Message);
    }

    [Fact]
    public void UpdateDetails_WithValidChanges_ShouldUpdateProperties()
    {
        var inst = Instrument.Create(_validCode, "Nombre Original", "Descripción Original", "Propósito Original");

        inst.UpdateDetails("Nombre Modificado", "Descripción Nueva", "Nuevo Propósito");

        Assert.Equal("Nombre Modificado", inst.Name);
        Assert.Equal("Descripción Nueva", inst.Description);
        Assert.Equal("Nuevo Propósito", inst.Purpose);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDetails_WithInvalidName_ShouldThrowDomainException(string? invalidName)
    {
        var inst = Instrument.Create(_validCode, "Nombre", "Descripción", "Propósito");

        var ex = Assert.Throws<DomainException>(() => inst.UpdateDetails(invalidName!, "Descripción", "Propósito"));
        Assert.Contains("nombre del instrumento no puede estar vacío", ex.Message);
    }

    [Fact]
    public void Rehydrate_ShouldPreserveAllProperties()
    {
        var id = Guid.NewGuid();

        var inst = Instrument.Rehydrate(id, _validCode, "PHQ-9", "Desc", "Purpose");

        Assert.Equal(id, inst.InstrumentId);
        Assert.Equal(_validCode, inst.Code);
        Assert.Equal("PHQ-9", inst.Name);
        Assert.Equal("Desc", inst.Description);
        Assert.Equal("Purpose", inst.Purpose);
    }
}
