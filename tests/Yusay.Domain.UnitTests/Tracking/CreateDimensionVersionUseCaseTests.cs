using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands;
using Yusay.Application.Tracking.Commands.CreateDimensionVersion;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

public class CreateDimensionVersionUseCaseTests
{
    private static readonly IReadOnlyList<AnchorInput> DefaultAnchors =
        [new AnchorInput(1, "Bajo"), new AnchorInput(3, "Medio"), new AnchorInput(5, "Alto")];

    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDimensionAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly Dimension _dimension;

    public CreateDimensionVersionUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
        _dimension = Dimension.Create(
            DimensionCode.Create("mood"), "Ánimo", "Estado subjetivo del día.");
        _repository.Dimensions.Add(_dimension);
    }

    private CreateDimensionVersionUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    private CreateDimensionVersionCommand Command(
        int version = 1,
        string definition = "Percepción de ánimo del día.",
        int minValue = 1,
        int maxValue = 5,
        int step = 1,
        IReadOnlyList<AnchorInput>? anchors = null) =>
        new(_dimension.DimensionId, version, definition, minValue, maxValue, step,
            anchors ?? DefaultAnchors);

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutReading()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Equal(0, _repository.GetByIdForUpdateCalls);
        Assert.Empty(_repository.Versions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task ExecuteAsync_WithNonPositiveVersionNumber_ShouldThrowDomainException(int version)
    {
        await Assert.ThrowsAsync<DomainException>(
            () => CreateUseCase().ExecuteAsync(Command(version: version)));

        Assert.Empty(_repository.Versions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Theory]
    [InlineData(5, 5, 1)]
    [InlineData(3, 1, 1)]
    [InlineData(1, 5, 0)]
    [InlineData(1, 5, -2)]
    [InlineData(1, 5, 3)]
    public async Task ExecuteAsync_WithInvalidScale_ShouldThrowDomainException(
        int minValue, int maxValue, int step)
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            Command(minValue: minValue, maxValue: maxValue, step: step)));

        Assert.Empty(_repository.Versions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyDefinition_ShouldThrowDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(
            () => CreateUseCase().ExecuteAsync(Command(definition: "   ")));

        Assert.Empty(_repository.Versions);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnchorOutsideScale_ShouldThrowDomainException()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            Command(anchors: [new AnchorInput(1, "Bajo"), new AnchorInput(7, "Muy alto")])));

        Assert.Contains("no pertenece a la escala", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_repository.Versions);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnchorNotAlignedToStep_ShouldThrowDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            Command(step: 2, anchors: [new AnchorInput(1, "Bajo"), new AnchorInput(2, "Medio")])));

        Assert.Empty(_repository.Versions);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicatedAnchorValues_ShouldThrowDomainException()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            Command(anchors: [new AnchorInput(1, "Bajo"), new AnchorInput(1, "Otro")])));

        Assert.Contains("anclajes duplicados", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_repository.Versions);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyAnchorLabel_ShouldThrowDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            Command(anchors: [new AnchorInput(1, "  ")])));

        Assert.Empty(_repository.Versions);
    }

    [Fact]
    public async Task ExecuteAsync_ForUnknownDimension_ShouldThrowNotFoundWithoutInserting()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase()
            .ExecuteAsync(Command() with { DimensionId = Guid.NewGuid() }));

        Assert.Equal("La Dimensión solicitada no existe.", exception.Message);
        Assert.Empty(_repository.Versions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicatedVersionNumber_ShouldThrowConflictWithTheContractMessage()
    {
        _repository.Versions.Add(DimensionVersion.CreateDraft(
            _dimension.DimensionId, 2,
            DimensionScale.Create(1, 5, 1), "Versión previa.",
            [DimensionAnchor.Create(1, "Bajo")]));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase().ExecuteAsync(Command(version: 2)));

        Assert.Equal(
            IDimensionAdministrationRepository.DuplicateVersionNumberConflictMessage,
            exception.Message);
        Assert.Single(_repository.Versions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryRaceYieldsDuplicatedNumber_ShouldTranslateToTheSameConflict()
    {
        _repository.AddVersionRaceYieldsDuplicateNumber = true;

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Equal(
            IDimensionAdministrationRepository.DuplicateVersionNumberConflictMessage,
            exception.Message);
        Assert.Empty(_repository.Versions);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithEffectiveData_ShouldPersistDraftVersionWithAnchorsAndAuditCreated()
    {
        var result = await CreateUseCase().ExecuteAsync(
            Command(definition: "  Percepción del ánimo.  "));

        Assert.Equal(1, result.Version);
        Assert.Equal(DimensionVersionStatus.Draft, result.Status);
        Assert.Equal("Percepción del ánimo.", result.Definition);
        Assert.Equal(1, result.MinValue);
        Assert.Equal(5, result.MaxValue);
        Assert.Equal(1, result.Step);
        Assert.Equal([1, 3, 5], result.Anchors.Select(anchor => anchor.Value));
        Assert.Equal(["Bajo", "Medio", "Alto"], result.Anchors.Select(anchor => anchor.Label));

        var stored = Assert.Single(_repository.Versions);
        Assert.Equal(result.DimensionVersionId, stored.DimensionVersionId);
        Assert.Equal(DimensionVersionStatus.Draft, stored.Status);
        Assert.Equal(3, stored.Anchors.Count);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(_adminUserId, audit.ActorUserId);
        Assert.Equal("DIMENSION_VERSION", audit.TargetType);
        Assert.Equal(result.DimensionVersionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullAnchors_ShouldPersistZeroAnchors()
    {
        var result = await CreateUseCase().ExecuteAsync(new CreateDimensionVersionCommand(
            _dimension.DimensionId, 1, "Percepción de ánimo del día.", 1, 5, 1, null));

        Assert.Empty(result.Anchors);
        Assert.Empty(Assert.Single(_repository.Versions).Anchors);
        Assert.Null(Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyAnchorsCollection_ShouldPersistZeroAnchors()
    {
        var result = await CreateUseCase().ExecuteAsync(new CreateDimensionVersionCommand(
            _dimension.DimensionId, 1, "Percepción de ánimo del día.", 1, 5, 1, []));

        Assert.Empty(result.Anchors);
        Assert.Empty(Assert.Single(_repository.Versions).Anchors);
        Assert.Null(Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldLockTheDimensionBeforeTheVersionPreCheckAndTheInsert()
    {
        await CreateUseCase().ExecuteAsync(Command());

        Assert.True(_unitOfWork.TransactionExecuted);
        Assert.Equal(1, _repository.GetByIdForUpdateCalls);
        Assert.Equal(
            ["GetByIdForUpdate", "VersionNumberExists", "AddVersion"],
            _repository.Operations);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuditFails_ShouldPropagateWithoutAuditEvent()
    {
        _auditEventRepo.FailNextAdd = true;

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Empty(_auditEventRepo.Events);
    }
}
