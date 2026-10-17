using Yusay.Application.Assessment.Commands.CreateInstrument;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Common;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class CreateInstrumentUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeInstrumentAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();

    public CreateInstrumentUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
    }

    private CreateInstrumentUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    [Fact]
    public async Task ExecuteAsync_WithValidData_ShouldPersistInstrumentAndAuditCatalogCreated()
    {
        var result = await CreateUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9 Depresión", "Cuestionario de 9 ítems.", "Despistaje"));

        Assert.NotEqual(Guid.Empty, result.InstrumentId);
        Assert.Equal("phq-9", result.Code);
        Assert.Equal("PHQ-9 Depresión", result.Name);
        Assert.Equal("Cuestionario de 9 ítems.", result.Description);
        Assert.Equal("Despistaje", result.Purpose);

        var stored = Assert.Single(_repository.Instruments);
        Assert.Equal(result.InstrumentId, stored.InstrumentId);
        Assert.Equal("phq-9", stored.Code.Value);
        Assert.True(_unitOfWork.TransactionExecuted);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(_adminUserId, audit.ActorUserId);
        Assert.Equal("INSTRUMENT", audit.TargetType);
        Assert.Equal(result.InstrumentId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateCodeInPreCheck_ShouldThrowConflictExceptionBeforeTransaction()
    {
        await CreateUseCase().ExecuteAsync(
            new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateUseCase().ExecuteAsync(new CreateInstrumentCommand("phq-9", "Otro PHQ-9", "Otra desc", "Otro propósito")));

        Assert.Equal("Ya existe un instrumento con el código especificado.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidDomainFormat_ShouldThrowBeforeTransaction()
    {
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync(new CreateInstrumentCommand("código inválido!", "Nombre", "Desc", "Propósito")));

        Assert.Contains("no es válido", ex.Message);
        Assert.Empty(_repository.Instruments);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorizedException()
    {
        _currentUser.UserId = null;

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateUseCase().ExecuteAsync(new CreateInstrumentCommand("phq-9", "PHQ-9", "Desc", "Propósito")));

        Assert.Contains("autenticada", ex.Message);
    }
}
