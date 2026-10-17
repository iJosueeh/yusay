using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands.ActivateDimensionVersion;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

public class ActivateDimensionVersionUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDimensionAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly Dimension _dimension;
    private DimensionVersion _version;

    public ActivateDimensionVersionUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
        _dimension = Dimension.Create(
            DimensionCode.Create("mood"), "Ánimo", "Estado subjetivo del día.");
        _repository.Dimensions.Add(_dimension);
        _version = SeedVersion(DimensionVersionStatus.Draft, 1);
    }

    private ActivateDimensionVersionUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    private ActivateDimensionVersionCommand Command(
        Guid? dimensionId = null, Guid? versionId = null) =>
        new(dimensionId ?? _dimension.DimensionId, versionId ?? _version.DimensionVersionId);

    private DimensionVersion SeedVersion(DimensionVersionStatus status, int version)
    {
        var stored = DimensionVersion.CreateDraft(
            _dimension.DimensionId,
            version,
            DimensionScale.Create(1, 5, 1),
            "Percepción de ánimo del día.",
            [DimensionAnchor.Create(1, "Bajo"), DimensionAnchor.Create(3, "Medio"),
                DimensionAnchor.Create(5, "Alto")]);

        if (status != DimensionVersionStatus.Draft)
        {
            stored = DimensionVersion.Rehydrate(
                stored.DimensionVersionId, stored.DimensionId, stored.Version, stored.Scale,
                stored.Definition, status, stored.Anchors);
        }

        _repository.Versions.Add(stored);
        return stored;
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutReading()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Equal(0, _repository.GetByIdForUpdateCalls);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ForUnknownDimension_ShouldThrowNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => CreateUseCase().ExecuteAsync(Command(dimensionId: Guid.NewGuid())));

        Assert.Equal("La Dimensión solicitada no existe.", exception.Message);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ForUnknownVersion_ShouldThrowNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => CreateUseCase().ExecuteAsync(Command(versionId: Guid.NewGuid())));

        Assert.Equal("La versión de Dimensión solicitada no existe.", exception.Message);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ForVersionBelongingToAnotherDimension_ShouldThrowNotFound()
    {
        var otherDimension = Dimension.Create(
            DimensionCode.Create("stress"), "Estrés", "Nivel de tensión percibido.");
        _repository.Dimensions.Add(otherDimension);
        var foreign = DimensionVersion.CreateDraft(
            otherDimension.DimensionId, 1, DimensionScale.Create(1, 5, 1), "Versión ajena.",
            [DimensionAnchor.Create(1, "Bajo")]);
        _repository.Versions.Add(foreign);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => CreateUseCase().ExecuteAsync(Command(versionId: foreign.DimensionVersionId)));

        Assert.Equal("La versión de Dimensión solicitada no existe.", exception.Message);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Theory]
    [InlineData(DimensionVersionStatus.Active)]
    [InlineData(DimensionVersionStatus.Retired)]
    public async Task ExecuteAsync_ForNonDraftVersion_ShouldThrowConflictWithoutCheckingActive(
        DimensionVersionStatus status)
    {
        _repository.Versions.Clear();
        _version = SeedVersion(status, 1);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Equal(
            "La versión de la Dimensión solo puede activarse mientras está en estado DRAFT.",
            exception.Message);
        Assert.Equal(["GetByIdForUpdate", "GetVersionForUpdate"], _repository.Operations);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(status, Assert.Single(_repository.Versions).Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnotherVersionIsActive_ShouldThrowConflictWithoutRetiringIt()
    {
        var activeVersion = SeedVersion(DimensionVersionStatus.Active, 2);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Equal(
            IDimensionAdministrationRepository.AnotherActiveVersionConflictMessage,
            exception.Message);
        Assert.Equal(
            ["GetByIdForUpdate", "GetVersionForUpdate", "GetActiveVersionId"],
            _repository.Operations);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(DimensionVersionStatus.Draft,
            _repository.Versions.Single(v => v.DimensionVersionId == _version.DimensionVersionId).Status);
        Assert.Equal(DimensionVersionStatus.Active,
            _repository.Versions.Single(v => v.DimensionVersionId == activeVersion.DimensionVersionId).Status);
    }

    [Fact]
    public async Task ExecuteAsync_WithDraftVersion_ShouldActivatePreserveTheRepresentationAndAudit()
    {
        var result = await CreateUseCase().ExecuteAsync(Command());

        Assert.Equal(DimensionVersionStatus.Active, result.Status);
        Assert.Equal(1, result.Version);
        Assert.Equal("Percepción de ánimo del día.", result.Definition);
        Assert.Equal(1, result.MinValue);
        Assert.Equal(5, result.MaxValue);
        Assert.Equal(1, result.Step);
        Assert.Equal([1, 3, 5], result.Anchors.Select(anchor => anchor.Value));

        Assert.Equal(
            ["GetByIdForUpdate", "GetVersionForUpdate", "GetActiveVersionId", "UpdateStatus"],
            _repository.Operations);

        var stored = Assert.Single(_repository.Versions);
        Assert.Equal(DimensionVersionStatus.Active, stored.Status);
        Assert.Equal(1, stored.Version);
        Assert.Equal("Percepción de ánimo del día.", stored.Definition);
        Assert.Equal([1, 3, 5], stored.Anchors.Select(anchor => anchor.Value));

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("DIMENSION_VERSION_ACTIVATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(_adminUserId, audit.ActorUserId);
        Assert.Equal("DIMENSION_VERSION", audit.TargetType);
        Assert.Equal(_version.DimensionVersionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStatusWriteAffectsNoRow_ShouldThrowWithoutAuditing()
    {
        _repository.UpdateStatusReturnsNoRows = true;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Contains("no afectó a ninguna fila", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(DimensionVersionStatus.Draft, Assert.Single(_repository.Versions).Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheUniqueActiveIndexRaceFires_ShouldSurfaceTheContractConflictMessage()
    {
        _repository.ActiveVersionRaceYieldsConflict = true;

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Equal(
            IDimensionAdministrationRepository.AnotherActiveVersionConflictMessage,
            exception.Message);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheStatusWriteFails_ShouldPropagateWithoutAuditing()
    {
        _repository.UpdateStatusFailure = new InvalidOperationException("PostgreSQL inaccesible.");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Equal("PostgreSQL inaccesible.", exception.Message);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(DimensionVersionStatus.Draft, Assert.Single(_repository.Versions).Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuditFails_ShouldPropagate()
    {
        _auditEventRepo.FailNextAdd = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnchorOutsideTheScale_ShouldThrowDomainExceptionBeforeWriting()
    {
        _repository.Versions.Clear();
        var corrupted = DimensionVersion.Rehydrate(
            _version.DimensionVersionId, _dimension.DimensionId, 1,
            DimensionScale.Create(1, 5, 1), "Versión con anclaje corrupto.",
            DimensionVersionStatus.Draft,
            [DimensionAnchor.Create(7, "Muy alto")]);
        _repository.Versions.Add(corrupted);

        var exception = await Assert.ThrowsAsync<DomainException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Contains("no pertenece a la escala", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(_repository.Operations,
            operation => operation == "UpdateStatus");
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(DimensionVersionStatus.Draft, Assert.Single(_repository.Versions).Status);
    }
}
