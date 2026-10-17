using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Commands;
using Yusay.Application.Tracking.Commands.UpdateDimensionVersion;
using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

public class UpdateDimensionVersionUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeDimensionAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly Dimension _dimension;
    private DimensionVersion _version;

    public UpdateDimensionVersionUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
        _dimension = Dimension.Create(
            DimensionCode.Create("mood"), "Ánimo", "Estado subjetivo del día.");
        _repository.Dimensions.Add(_dimension);
        _version = SeedVersion(DimensionVersionStatus.Draft, 1, "Percepción de ánimo del día.",
            [new DimensionAnchorDto(1, "Bajo"), new DimensionAnchorDto(3, "Medio"),
                new DimensionAnchorDto(5, "Alto")]);
    }

    private UpdateDimensionVersionUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    private UpdateDimensionVersionCommand Command(
        string? definition = null,
        int? minValue = null,
        int? maxValue = null,
        int? step = null,
        IReadOnlyList<AnchorInput>? anchors = null,
        Guid? dimensionId = null,
        Guid? versionId = null) =>
        new(dimensionId ?? _dimension.DimensionId,
            versionId ?? _version.DimensionVersionId,
            definition, minValue, maxValue, step, anchors);

    private DimensionVersion SeedVersion(
        DimensionVersionStatus status,
        int version,
        string definition,
        IReadOnlyList<DimensionAnchorDto> anchors)
    {
        var stored = DimensionVersion.CreateDraft(
            _dimension.DimensionId,
            version,
            DimensionScale.Create(1, 5, 1),
            definition,
            anchors.Select(anchor => DimensionAnchor.Create(anchor.Value, anchor.Label)));

        if (status != DimensionVersionStatus.Draft)
        {
            stored = DimensionVersion.Rehydrate(
                stored.DimensionVersionId, stored.DimensionId, stored.Version, stored.Scale,
                stored.Definition, status, stored.Anchors);
        }

        _repository.Versions.Add(stored);
        return stored;
    }

    public readonly record struct DimensionAnchorDto(int Value, string Label);

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutReading()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(Command(definition: "Nueva definición.")));

        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Equal(0, _repository.GetByIdForUpdateCalls);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ForUnknownDimension_ShouldThrowNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase()
            .ExecuteAsync(Command(definition: "Nueva definición.", dimensionId: Guid.NewGuid())));

        Assert.Equal("La Dimensión solicitada no existe.", exception.Message);
        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ForUnknownVersion_ShouldThrowNotFound()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase()
            .ExecuteAsync(Command(definition: "Nueva definición.", versionId: Guid.NewGuid())));

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

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => CreateUseCase()
            .ExecuteAsync(Command(definition: "Nueva definición.", versionId: foreign.DimensionVersionId)));

        Assert.Equal("La versión de Dimensión solicitada no existe.", exception.Message);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Theory]
    [InlineData(DimensionVersionStatus.Active)]
    [InlineData(DimensionVersionStatus.Retired)]
    public async Task ExecuteAsync_ForNonDraftVersion_ShouldThrowConflict(
        DimensionVersionStatus status)
    {
        _repository.Versions.Clear();
        _version = SeedVersion(status, 1, "Percepción de ánimo del día.",
            [new DimensionAnchorDto(1, "Bajo")]);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase().ExecuteAsync(Command(definition: "Nueva definición.")));

        Assert.Equal(
            "La versión de la Dimensión solo puede editarse mientras está en estado DRAFT.",
            exception.Message);
        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_ForNonDraftVersion_ShouldRejectBeforeCheckingEffectiveChanges()
    {
        _repository.Versions.Clear();
        _version = SeedVersion(DimensionVersionStatus.Active, 1, "Percepción de ánimo del día.",
            [new DimensionAnchorDto(1, "Bajo")]);

        await Assert.ThrowsAsync<ConflictException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Empty(_repository.Updated);
    }

    [Theory]
    [InlineData(3, null, null)]
    [InlineData(null, 7, null)]
    [InlineData(null, null, 2)]
    [InlineData(3, 7, null)]
    public async Task ExecuteAsync_WithPartialScale_ShouldThrowValidationWithoutOpeningTheTransaction(
        int? minValue, int? maxValue, int? step)
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase()
            .ExecuteAsync(Command(minValue: minValue, maxValue: maxValue, step: step)));

        Assert.Contains("atómico", exception.Message, StringComparison.Ordinal);
        Assert.False(_unitOfWork.TransactionExecuted);
        Assert.Empty(_repository.Operations);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithDefinitionChange_ShouldUpdateAndAuditDefinitionOnly()
    {
        var result = await CreateUseCase().ExecuteAsync(
            Command(definition: "  Percepción del ánimo del día.  "));

        Assert.Equal("Percepción del ánimo del día.", result.Definition);
        Assert.Equal("Percepción del ánimo del día.", Assert.Single(_repository.Versions).Definition);
        Assert.Equal(["GetByIdForUpdate", "GetVersionForUpdate", "UpdateVersionDetails"],
            _repository.Operations);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(_adminUserId, audit.ActorUserId);
        Assert.Equal("DIMENSION_VERSION", audit.TargetType);
        Assert.Equal(_version.DimensionVersionId.ToString(), audit.TargetIdentifier);
        Assert.Equal("""{"changed_fields":["definition"]}""", audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithScaleChange_ShouldAuditOnlyTheScaleFieldsReallyChanged()
    {
        var result = await CreateUseCase().ExecuteAsync(
            Command(minValue: 1, maxValue: 7, step: 1));

        Assert.Equal(7, result.MaxValue);
        Assert.Equal(7, Assert.Single(_repository.Versions).Scale.MaxValue);
        Assert.Equal("""{"changed_fields":["max_value"]}""",
            Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithDefinitionAndScaleChange_ShouldAuditAllChangedFieldsInOrder()
    {
        await CreateUseCase().ExecuteAsync(
            Command(definition: "Nueva definición.", minValue: 0, maxValue: 5, step: 1));

        Assert.Equal("""{"changed_fields":["definition","min_value"]}""",
            Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutEffectiveChanges_ShouldThrowValidationWithoutWriting()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase().ExecuteAsync(Command()));

        Assert.Contains("no modifica ningún campo", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["GetByIdForUpdate", "GetVersionForUpdate"], _repository.Operations);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal("Percepción de ánimo del día.", Assert.Single(_repository.Versions).Definition);
    }

    [Fact]
    public async Task ExecuteAsync_WithSameDefinitionIncludingPadding_ShouldRejectAsNoOp()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase()
            .ExecuteAsync(Command(definition: "  Percepción de ánimo del día.  ")));

        Assert.Contains("no modifica ningún campo", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithSameAnchorsIncludingPaddedLabels_ShouldRejectAsNoOp()
    {
        var exception = await Assert.ThrowsAsync<ValidationException>(() => CreateUseCase()
            .ExecuteAsync(Command(anchors:
            [
                new AnchorInput(1, "  Bajo  "), new AnchorInput(3, "Medio"),
                new AnchorInput(5, "Alto")
            ])));

        Assert.Contains("no modifica ningún campo", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnchorReplacement_ShouldDeleteRelabelInsertAndAuditNullMetadata()
    {
        var result = await CreateUseCase().ExecuteAsync(Command(anchors:
        [
            new AnchorInput(1, "Muy bajo"), new AnchorInput(2, "Bajo-medio"),
            new AnchorInput(5, "Alto")
        ]));

        Assert.Equal([1, 2, 5], result.Anchors.Select(anchor => anchor.Value));
        Assert.Equal(["Muy bajo", "Bajo-medio", "Alto"],
            result.Anchors.Select(anchor => anchor.Label));
        Assert.Equal(
            ["GetByIdForUpdate", "GetVersionForUpdate", "DeleteAnchors", "UpdateAnchorLabel",
                "InsertAnchors"],
            _repository.Operations);

        var stored = Assert.Single(_repository.Versions);
        Assert.Equal([1, 2, 5], stored.Anchors.Select(anchor => anchor.Value));

        Assert.Null(Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyAnchors_ShouldRemoveTheWholeSetAndAuditNullMetadata()
    {
        var result = await CreateUseCase().ExecuteAsync(Command(anchors: []));

        Assert.Empty(result.Anchors);
        Assert.Empty(Assert.Single(_repository.Versions).Anchors);
        Assert.Equal(
            ["GetByIdForUpdate", "GetVersionForUpdate", "DeleteAnchors"],
            _repository.Operations);
        Assert.Null(Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAnchorsField_ShouldConserveTheStoredSet()
    {
        var result = await CreateUseCase().ExecuteAsync(Command(definition: "Nueva definición."));

        Assert.Equal([1, 3, 5], result.Anchors.Select(anchor => anchor.Value));
        Assert.Equal(3, Assert.Single(_repository.Versions).Anchors.Count);
        Assert.DoesNotContain(_repository.Operations,
            operation => operation.StartsWith("Delete", StringComparison.Ordinal) ||
                operation.StartsWith("Insert", StringComparison.Ordinal) ||
                operation.StartsWith("UpdateAnchor", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_WithScaleShrinkAndConservedIncompatibleAnchors_ShouldThrowDomainException()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateUseCase()
            .ExecuteAsync(Command(minValue: 1, maxValue: 3, step: 1)));

        Assert.Contains("no pertenece a la escala", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["GetByIdForUpdate", "GetVersionForUpdate"], _repository.Operations);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(5, Assert.Single(_repository.Versions).Scale.MaxValue);
    }

    [Fact]
    public async Task ExecuteAsync_WithCombinedScaleAndAnchorsChange_ShouldApplyTheAtomicCandidate()
    {
        var result = await CreateUseCase().ExecuteAsync(
            Command(minValue: 1, maxValue: 3, step: 1, anchors:
            [
                new AnchorInput(1, "Bajo"), new AnchorInput(2, "Medio"),
                new AnchorInput(3, "Alto")
            ]));

        Assert.Equal(1, result.MinValue);
        Assert.Equal(3, result.MaxValue);
        Assert.Equal(1, result.Step);
        Assert.Equal([1, 2, 3], result.Anchors.Select(anchor => anchor.Value));
        Assert.Equal(
            ["GetByIdForUpdate", "GetVersionForUpdate", "DeleteAnchors", "UpdateVersionDetails",
                "UpdateAnchorLabel", "InsertAnchors"],
            _repository.Operations);

        var stored = Assert.Single(_repository.Versions);
        Assert.Equal(3, stored.Scale.MaxValue);
        Assert.Equal([1, 2, 3], stored.Anchors.Select(anchor => anchor.Value));
        Assert.Equal("Alto", Assert.Single(stored.Anchors, anchor => anchor.Value == 3).Label);

        Assert.Equal("""{"changed_fields":["max_value"]}""",
            Assert.Single(_auditEventRepo.Events).Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateAnchorsInRequest_ShouldThrowDomainException()
    {
        var exception = await Assert.ThrowsAsync<DomainException>(() => CreateUseCase()
            .ExecuteAsync(Command(anchors:
            [
                new AnchorInput(1, "Bajo"), new AnchorInput(1, "Otro")
            ])));

        Assert.Contains("anclajes duplicados", exception.Message, StringComparison.Ordinal);
        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithWhitespaceAnchorLabel_ShouldThrowDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase()
            .ExecuteAsync(Command(anchors:
            [
                new AnchorInput(1, "Bajo"), new AnchorInput(3, "  "), new AnchorInput(5, "Alto")
            ])));

        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnchorOutsideTheNewScale_ShouldThrowDomainException()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateUseCase().ExecuteAsync(
            Command(minValue: 1, maxValue: 3, step: 1, anchors:
            [
                new AnchorInput(1, "Bajo"), new AnchorInput(5, "Alto")
            ])));

        Assert.Empty(_repository.Updated);
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuditFails_ShouldPropagate()
    {
        _auditEventRepo.FailNextAdd = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateUseCase()
            .ExecuteAsync(Command(definition: "Nueva definición.")));

        Assert.Empty(_auditEventRepo.Events);
    }
}
