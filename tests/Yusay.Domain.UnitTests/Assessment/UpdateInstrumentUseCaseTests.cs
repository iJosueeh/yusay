using Yusay.Application.Assessment.Commands.CreateInstrument;
using Yusay.Application.Assessment.Commands.UpdateInstrument;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Common;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class UpdateInstrumentUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeInstrumentAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();

    public UpdateInstrumentUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
    }

    private CreateInstrumentUseCase CreateInstrumentUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    private UpdateInstrumentUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    [Fact]
    public async Task ExecuteAsync_WithChangedFields_ShouldUpdateAndAuditCatalogUpdated()
    {
        var created = await CreateInstrumentUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc inicial", "Propósito inicial"));
        _auditEventRepo.Events.Clear();

        var updated = await CreateUseCase().ExecuteAsync(
            new UpdateInstrumentCommand(created.InstrumentId, "PHQ-9 Actualizado", "Desc inicial", "Nuevo propósito"));

        Assert.Equal("PHQ-9 Actualizado", updated.Name);
        Assert.Equal("Desc inicial", updated.Description);
        Assert.Equal("Nuevo propósito", updated.Purpose);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("INSTRUMENT", audit.TargetType);
        Assert.Equal(created.InstrumentId.ToString(), audit.TargetIdentifier);
        Assert.NotNull(audit.Metadata);
        Assert.Contains("name", audit.Metadata);
        Assert.Contains("purpose", audit.Metadata);
        Assert.DoesNotContain("description", audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoChanges_ShouldNotEmitAuditEvent()
    {
        var created = await CreateInstrumentUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));
        _auditEventRepo.Events.Clear();

        var updated = await CreateUseCase().ExecuteAsync(
            new UpdateInstrumentCommand(created.InstrumentId, "PHQ-9", "Desc", "Propósito"));

        Assert.Equal(created.InstrumentId, updated.InstrumentId);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentInstrument_ShouldThrowNotFoundException()
    {
        var missingId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateUseCase().ExecuteAsync(new UpdateInstrumentCommand(missingId, "Nombre", "Desc", null)));

        Assert.Contains("no existe", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidDomainName_ShouldThrowBeforeTransaction()
    {
        var created = await CreateInstrumentUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync(new UpdateInstrumentCommand(created.InstrumentId, "  ", "Desc", "Propósito")));

        Assert.Contains("nombre del instrumento no puede estar vacío", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorizedException()
    {
        _currentUser.UserId = null;

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateUseCase().ExecuteAsync(new UpdateInstrumentCommand(Guid.NewGuid(), "PHQ-9", "Desc", "Propósito")));

        Assert.Contains("autenticada", ex.Message);
    }
}
