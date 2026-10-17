using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Assessment;

public class InstrumentVersionTests
{
    private readonly Guid _instrumentId = Guid.NewGuid();

    [Fact]
    public void CreateDraft_WithValidParameters_ShouldInstantiateInDraftStatus()
    {
        var ver = InstrumentVersion.CreateDraft(
            _instrumentId,
            1,
            "Kroenke K, Spitzer RL, Williams JB. The PHQ-9. J Gen Intern Med. 2001.",
            "Población adulta general y atención primaria.",
            "Autoadministrado en ambiente tranquilo.",
            "Dominio público para uso clínico.",
            "No reemplaza entrevista diagnóstica estructurada.");

        Assert.NotEqual(Guid.Empty, ver.InstrumentVersionId);
        Assert.Equal(_instrumentId, ver.InstrumentId);
        Assert.Equal(1, ver.Version);
        Assert.Equal(InstrumentVersionStatus.Draft, ver.Status);
        Assert.Equal("Kroenke K, Spitzer RL, Williams JB. The PHQ-9. J Gen Intern Med. 2001.", ver.SourceDescription);
        Assert.Equal("Población adulta general y atención primaria.", ver.Population);
        Assert.Equal("Autoadministrado en ambiente tranquilo.", ver.AdministrationConditions);
        Assert.Equal("Dominio público para uso clínico.", ver.LicenseInformation);
        Assert.Equal("No reemplaza entrevista diagnóstica estructurada.", ver.Limitations);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-10)]
    public void CreateDraft_WithInvalidVersionNumber_ShouldThrowDomainException(int invalidVersion)
    {
        var ex = Assert.Throws<DomainException>(() =>
            InstrumentVersion.CreateDraft(_instrumentId, invalidVersion, "Fuente", "Población", null, null, "Limitaciones"));

        Assert.Contains("entero positivo mayor a cero", ex.Message);
    }

    [Fact]
    public void CreateDraft_WithEmptyInstrumentId_ShouldThrowDomainException()
    {
        var ex = Assert.Throws<DomainException>(() =>
            InstrumentVersion.CreateDraft(Guid.Empty, 1, "Fuente", "Población", null, null, "Limitaciones"));

        Assert.Contains("UUID no vacío", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_WithInvalidSourceDescription_ShouldThrowDomainException(string? invalidSource)
    {
        var ex = Assert.Throws<DomainException>(() =>
            InstrumentVersion.CreateDraft(_instrumentId, 1, invalidSource!, "Población", null, null, "Limitaciones"));

        Assert.Contains("fuente bibliográfica no puede estar vacía", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_WithInvalidPopulation_ShouldThrowDomainException(string? invalidPopulation)
    {
        var ex = Assert.Throws<DomainException>(() =>
            InstrumentVersion.CreateDraft(_instrumentId, 1, "Fuente", invalidPopulation!, null, null, "Limitaciones"));

        Assert.Contains("población objetivo no puede estar vacía", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_WithInvalidLimitations_ShouldThrowDomainException(string? invalidLimitations)
    {
        var ex = Assert.Throws<DomainException>(() =>
            InstrumentVersion.CreateDraft(_instrumentId, 1, "Fuente", "Población", null, null, invalidLimitations!));

        Assert.Contains("limitaciones psicométricas", ex.Message);
    }

    [Fact]
    public void Rehydrate_ShouldPreserveAllProperties()
    {
        var versionId = Guid.NewGuid();

        var ver = InstrumentVersion.Rehydrate(
            versionId,
            _instrumentId,
            2,
            InstrumentVersionStatus.Retired,
            "Fuente",
            "Población",
            "Condiciones",
            "Licencia",
            "Limitaciones");

        Assert.Equal(versionId, ver.InstrumentVersionId);
        Assert.Equal(_instrumentId, ver.InstrumentId);
        Assert.Equal(2, ver.Version);
        Assert.Equal(InstrumentVersionStatus.Retired, ver.Status);
        Assert.Equal("Fuente", ver.SourceDescription);
        Assert.Equal("Población", ver.Population);
        Assert.Equal("Condiciones", ver.AdministrationConditions);
        Assert.Equal("Licencia", ver.LicenseInformation);
        Assert.Equal("Limitaciones", ver.Limitations);
    }
}
