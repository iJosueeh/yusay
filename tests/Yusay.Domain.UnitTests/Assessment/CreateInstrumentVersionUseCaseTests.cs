using Yusay.Application.Assessment.Commands.CreateInstrument;
using Yusay.Application.Assessment.Commands.CreateInstrumentVersion;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Common;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class CreateInstrumentVersionUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeInstrumentAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();

    public CreateInstrumentVersionUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
    }

    private CreateInstrumentUseCase CreateInstrumentUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    private CreateInstrumentVersionUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    [Fact]
    public async Task ExecuteAsync_WithExplicitVersion_ShouldPersistDraftAndAuditCatalogCreated()
    {
        var instrument = await CreateInstrumentUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));
        _auditEventRepo.Events.Clear();

        var version = await CreateUseCase().ExecuteAsync(
            new CreateInstrumentVersionCommand(
                instrument.InstrumentId,
                1,
                "Kroenke et al. 2001",
                "Adultos",
                "Autoadministrado",
                "Público",
                "Despistaje, no diagnóstico definitivo"));

        Assert.NotEqual(Guid.Empty, version.InstrumentVersionId);
        Assert.Equal(instrument.InstrumentId, version.InstrumentId);
        Assert.Equal(1, version.Version);
        Assert.Equal("DRAFT", version.Status);
        Assert.Equal("Kroenke et al. 2001", version.SourceDescription);
        Assert.Equal("Adultos", version.Population);
        Assert.Equal("Autoadministrado", version.AdministrationConditions);
        Assert.Equal("Público", version.LicenseInformation);
        Assert.Equal("Despistaje, no diagnóstico definitivo", version.Limitations);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("INSTRUMENT_VERSION", audit.TargetType);
        Assert.Equal(version.InstrumentVersionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullVersion_ShouldAutoIncrementStartingAt1()
    {
        var instrument = await CreateInstrumentUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));

        var v1 = await CreateUseCase().ExecuteAsync(
            new CreateInstrumentVersionCommand(instrument.InstrumentId, null, "Fuente 1", "Población 1", null, null, "Limitaciones 1"));
        Assert.Equal(1, v1.Version);

        var v2 = await CreateUseCase().ExecuteAsync(
            new CreateInstrumentVersionCommand(instrument.InstrumentId, null, "Fuente 2", "Población 2", null, null, "Limitaciones 2"));
        Assert.Equal(2, v2.Version);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateVersionNumber_ShouldThrowConflictException()
    {
        var instrument = await CreateInstrumentUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));

        await CreateUseCase().ExecuteAsync(
            new CreateInstrumentVersionCommand(instrument.InstrumentId, 1, "Fuente", "Población", null, null, "Limitaciones"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateUseCase().ExecuteAsync(
                new CreateInstrumentVersionCommand(instrument.InstrumentId, 1, "Fuente 2", "Población 2", null, null, "Limitaciones 2")));

        Assert.Equal("Ya existe una versión con el número especificado para este instrumento.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentInstrument_ShouldThrowNotFoundException()
    {
        var missingId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateUseCase().ExecuteAsync(
                new CreateInstrumentVersionCommand(missingId, 1, "Fuente", "Población", null, null, "Limitaciones")));

        Assert.Contains("no existe", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidVersionNumber_ShouldThrowDomainException()
    {
        var instrument = await CreateInstrumentUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync(
                new CreateInstrumentVersionCommand(instrument.InstrumentId, 0, "Fuente", "Población", null, null, "Limitaciones")));

        Assert.Contains("entero positivo mayor a cero", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorizedException()
    {
        _currentUser.UserId = null;

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateUseCase().ExecuteAsync(
                new CreateInstrumentVersionCommand(Guid.NewGuid(), 1, "Fuente", "Población", null, null, "Limitaciones")));

        Assert.Contains("autenticada", ex.Message);
    }
}
