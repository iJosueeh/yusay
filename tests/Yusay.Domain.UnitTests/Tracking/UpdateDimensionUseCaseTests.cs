using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands.UpdateDimension;
using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

// B5a de RF-021: PATCH /admin/dimensions/{dimensionId}. La edición se realiza sobre el
// estado leído con FOR UPDATE (GetByIdForUpdateAsync), detecta cambios efectivos de forma
// canónica (trim), conserva los campos ausentes o null, mantiene el code inmutable y
// registra un único CATALOG_UPDATED (perfil C de V011) cuyo changed_fields solo contiene
// name y/o description según los cambios realmente aplicados. Sin cambios efectivos -> 400
// sin escritura ni auditoría.
public class UpdateDimensionUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDimensionAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly Dimension _dimension;

    public UpdateDimensionUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
        _dimension = Dimension.Create(
            DimensionCode.Create("mood"), "Ánimo", "Estado subjetivo del día.");
        _repository.Dimensions.Add(_dimension);
    }

    private UpdateDimensionUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    [Fact]
    public async Task ExecuteAsync_WithEffectiveNameChange_ShouldUpdateAndAuditChangedFields()
    {
        var result = await CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "Estado de ánimo", null));

        Assert.Equal(_dimension.DimensionId, result.DimensionId);
        Assert.Equal("mood", result.Code);
        Assert.Equal("Estado de ánimo", result.Name);
        Assert.Equal("Estado subjetivo del día.", result.Description);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(_adminUserId, audit.ActorUserId);
        Assert.Equal("DIMENSION", audit.TargetType);
        Assert.Equal(_dimension.DimensionId.ToString(), audit.TargetIdentifier);
        Assert.Equal("""{"changed_fields":["name"]}""", audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithDescriptionOnly_ShouldAuditDescriptionOnly()
    {
        await CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, null, "Nuevo criterio operativo."));

        Assert.Equal("Ánimo", Assert.Single(_repository.Dimensions).Name);
        Assert.Equal("""{"changed_fields":["description"]}""",
            Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithBothFields_ShouldAuditBothChangedFieldsInOrder()
    {
        await CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "Ánimo general", "Nueva descripción."));

        Assert.Equal("""{"changed_fields":["name","description"]}""",
            Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithOnlyOneEffectiveChangeOutOfTwoFields_ShouldAuditJustThatField()
    {
        // El name enviado coincide con el almacenado: solo la descripción es cambio efectivo
        await CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "Ánimo", "Nueva descripción."));

        Assert.Equal("""{"changed_fields":["description"]}""",
            Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithAbsentOrNullFields_ShouldRejectAsNoOpWithoutWriting()
    {
        var useCase = CreateUseCase();

        var absent = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, null, null)));

        Assert.Contains("no modifica ningún campo", absent.Message, StringComparison.Ordinal);
        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithSameValuesIncludingPadding_ShouldRejectAsNoOp()
    {
        // La comparación es canónica (trim): reenviar el mismo valor con espacios no cuenta
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "  Ánimo  ", "  Estado subjetivo del día.  ")));

        Assert.Contains("no modifica ningún campo", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceName_ShouldThrowDomainExceptionWithoutWriting()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "   ", null)));

        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ForUnknownDimension_ShouldThrowNotFoundWithoutAudit()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(Guid.NewGuid(), "Nuevo nombre", null)));

        Assert.Equal("La Dimensión solicitada no existe.", exception.Message);
        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutReading()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "Nuevo nombre", null)));

        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Equal(0, _repository.GetByIdForUpdateCalls);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReadThroughForUpdateInsideTheTransaction()
    {
        await CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "Estado de ánimo", null));

        // La edición observa el estado bloqueado: una única lectura FOR UPDATE por operación
        Assert.True(_unitOfWork.TransactionExecuted);
        Assert.Equal(1, _repository.GetByIdForUpdateCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPersistOnlyNameAndDescriptionAndNeverTheCode()
    {
        await CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "Estado de ánimo", "Nuevo criterio operativo."));

        var stored = Assert.Single(_repository.Dimensions);
        Assert.Equal(_dimension.DimensionId, stored.DimensionId);
        Assert.Equal("mood", stored.Code.Value);
        Assert.Equal("Estado de ánimo", stored.Name);
        Assert.Equal("Nuevo criterio operativo.", stored.Description);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuditFails_ShouldPropagate()
    {
        _auditEventRepo.FailNextAdd = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateUseCase().ExecuteAsync(
            new UpdateDimensionCommand(_dimension.DimensionId, "Estado de ánimo", null)));

        Assert.Empty(_auditEventRepo.Events);
    }
}
