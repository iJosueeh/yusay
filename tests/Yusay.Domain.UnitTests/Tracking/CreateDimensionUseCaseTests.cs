using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands.CreateDimension;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Common;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

// B5a de RF-021: POST /admin/dimensions. El caso de uso exige identidad autenticada,
// valida la forma en dominio ANTES de la transacción (400), resuelve el code duplicado con
// 409 (pre-check y traducción de 23505 con el mensaje único del contrato) y registra un
// único CATALOG_CREATED (actor ADMINISTRATOR, target DIMENSION, metadata NULL) en la misma
// transacción. La denegación a no administradores la resuelve la política del controlador.
public class CreateDimensionUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDimensionAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();

    public CreateDimensionUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
    }

    private CreateDimensionUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    [Fact]
    public async Task ExecuteAsync_WithValidData_ShouldPersistDimensionAndAuditCatalogCreated()
    {
        var result = await CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "Ánimo", "Estado subjetivo del día."));

        Assert.NotEqual(Guid.Empty, result.DimensionId);
        Assert.Equal("mood", result.Code);
        Assert.Equal("Ánimo", result.Name);
        Assert.Equal("Estado subjetivo del día.", result.Description);

        var stored = Assert.Single(_repository.Dimensions);
        Assert.Equal(result.DimensionId, stored.DimensionId);
        Assert.Equal("mood", stored.Code.Value);
        Assert.True(_unitOfWork.TransactionExecuted);

        // Un único audit_event: CATALOG_CREATED con perfil N (metadata NULL) de V011
        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(_adminUserId, audit.ActorUserId);
        Assert.Equal("DIMENSION", audit.TargetType);
        Assert.Equal(result.DimensionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldTrimNameAndDescription()
    {
        var result = await CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("energy", "  Energía  ", "  Nivel de energía percibido.  "));

        Assert.Equal("Energía", result.Name);
        Assert.Equal("Nivel de energía percibido.", result.Description);
        Assert.Equal("Energía", Assert.Single(_repository.Dimensions).Name);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutWriting()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "Ánimo", "Descripción.")));

        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Empty(_repository.Dimensions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidCode_ShouldThrowDomainExceptionWithoutTransaction()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("código inválido", "Ánimo", "Descripción.")));

        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Empty(_repository.Dimensions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyNameOrDescription_ShouldThrowDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "   ", "Descripción.")));
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "Ánimo", "")));

        Assert.Empty(_repository.Dimensions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicatedCode_ShouldThrowConflictWithoutAudit()
    {
        _repository.ExistingCodes.Add("mood");

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "Ánimo", "Descripción.")));

        Assert.Equal(IDimensionAdministrationRepository.DuplicateCodeConflictMessage, exception.Message);
        Assert.True(_unitOfWork.TransactionExecuted);
        Assert.Empty(_repository.Dimensions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryTranslatesTheInsertRace_ShouldSurfaceTheSameConflictMessage()
    {
        // El pre-check ve el code libre y la inserción pierde la carrera: el repositorio
        // traduce 23505 (uq_dimension_code) al mismo mensaje del pre-check (409 uniforme)
        _repository.AddRaceYieldsDuplicateCode = true;

        var exception = await Assert.ThrowsAsync<ConflictException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "Ánimo", "Descripción.")));

        Assert.Equal(IDimensionAdministrationRepository.DuplicateCodeConflictMessage, exception.Message);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithUntranslatedRepositoryFailure_ShouldPropagate()
    {
        _repository.AddFailure = new InvalidOperationException("Fallo físico sin traducir (500).");

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "Ánimo", "Descripción.")));

        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuditFails_ShouldPropagateWithoutResult()
    {
        // El alta y la auditoría comparten transacción (UoW real): el fallo de auditoría se
        // propaga y la transacción se revierte — la semántica de rollback se verifica en
        // integración con PostgreSQL real.
        _auditEventRepo.FailNextAdd = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateUseCase().ExecuteAsync(
            new CreateDimensionCommand("mood", "Ánimo", "Descripción.")));

        Assert.Empty(_auditEventRepo.Events);
    }
}
