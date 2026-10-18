using Yusay.Application.Assessment.Commands.ReplaceDraftDefinition;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Assessment.ValueObjects;
using Yusay.Domain.Common;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class ReplaceDraftDefinitionUseCaseTests
{
    private readonly Guid _adminUserId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeInstrumentAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();

    public ReplaceDraftDefinitionUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_adminUserId);
    }

    private ReplaceDraftDefinitionUseCase CreateUseCase() => new(
        _currentUser, _unitOfWork, _repository, _auditEventRepo);

    private (Instrument Instrument, InstrumentVersion Version) SeedInstrumentAndDraftVersion()
    {
        var instrument = Instrument.Create(
            InstrumentCode.Create("phq-9"),
            "PHQ-9 Depresión",
            "Cuestionario de 9 ítems.",
            "Despistaje");

        var version = InstrumentVersion.CreateDraft(
            instrument.InstrumentId,
            1,
            "Fuente oficial",
            "Población general adulta",
            "Autoadministrado",
            "Dominio público",
            "No sustituye entrevista diagnóstica");

        _repository.Instruments.Add(instrument);
        _repository.Versions.Add(version);

        return (instrument, version);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorizedException()
    {
        _currentUser.UserId = null;
        var (inst, ver) = SeedInstrumentAndDraftVersion();

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                inst.InstrumentId, ver.InstrumentVersionId, [], [], [])));

        Assert.Contains("usuario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInstrumentNotFound_ShouldThrowNotFoundException()
    {
        var (_, ver) = SeedInstrumentAndDraftVersion();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                Guid.NewGuid(), ver.InstrumentVersionId, [], [], [])));

        Assert.Contains("instrumento", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenVersionNotFound_ShouldThrowNotFoundException()
    {
        var (inst, _) = SeedInstrumentAndDraftVersion();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                inst.InstrumentId, Guid.NewGuid(), [], [], [])));

        Assert.Contains("versión", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenVersionBelongsToAnotherInstrument_ShouldThrowNotFoundException()
    {
        var (inst1, _) = SeedInstrumentAndDraftVersion();
        var (_, ver2) = SeedInstrumentAndDraftVersion();

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                inst1.InstrumentId, ver2.InstrumentVersionId, [], [], [])));

        Assert.Contains("versión", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WhenVersionStatusNotDraft_ShouldThrowConflictException()
    {
        var (inst, ver) = SeedInstrumentAndDraftVersion();
        var readyVersion = InstrumentVersion.Rehydrate(
            ver.InstrumentVersionId,
            inst.InstrumentId,
            ver.Version,
            InstrumentVersionStatus.Ready,
            ver.SourceDescription,
            ver.Population,
            ver.AdministrationConditions,
            ver.LicenseInformation,
            ver.Limitations);

        _repository.Versions.Remove(ver);
        _repository.Versions.Add(readyVersion);

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                inst.InstrumentId, ver.InstrumentVersionId, [], [], [])));

        Assert.Contains("DRAFT", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateReferenceOrders_ShouldThrowDomainException()
    {
        var (inst, ver) = SeedInstrumentAndDraftVersion();
        var references = new[]
        {
            new DraftReferenceInput(1, "Ref A", null),
            new DraftReferenceInput(1, "Ref B", null)
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                inst.InstrumentId, ver.InstrumentVersionId, references, [], [])));

        Assert.Contains("duplicado", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateQuestionPositions_ShouldThrowDomainException()
    {
        var (inst, ver) = SeedInstrumentAndDraftVersion();
        var questions = new[]
        {
            new DraftQuestionInput(1, "Pregunta 1", true, []),
            new DraftQuestionInput(1, "Pregunta duplicada", true, [])
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                inst.InstrumentId, ver.InstrumentVersionId, [], questions, [])));

        Assert.Contains("duplicada", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithDuplicateOptionPositionsInSameQuestion_ShouldThrowDomainException()
    {
        var (inst, ver) = SeedInstrumentAndDraftVersion();
        var questions = new[]
        {
            new DraftQuestionInput(1, "Pregunta 1", true,
            [
                new DraftOptionInput(1, "Opción A", 0),
                new DraftOptionInput(1, "Opción B", 1)
            ])
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
                inst.InstrumentId, ver.InstrumentVersionId, [], questions, [])));

        Assert.Contains("duplicada", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidNewComposition_ShouldPersistAndAuditCatalogUpdated()
    {
        var (inst, ver) = SeedInstrumentAndDraftVersion();

        var references = new[]
        {
            new DraftReferenceInput(1, "Kroenke et al. (2001)", "https://doi.org/10.1046/j.1525-1497.2001.016009606.x")
        };
        var questions = new[]
        {
            new DraftQuestionInput(1, "¿Poco interés o alegría por hacer las cosas?", true,
            [
                new DraftOptionInput(1, "Para nada", 0),
                new DraftOptionInput(2, "Varios días", 1),
                new DraftOptionInput(3, "Más de la mitad de los días", 2),
                new DraftOptionInput(4, "Casi todos los días", 3)
            ])
        };
        var interpretations = new[]
        {
            new DraftInterpretationInput("Mínima", "Depresión mínima", null, 0, 4)
        };

        var result = await CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
            inst.InstrumentId, ver.InstrumentVersionId, references, questions, interpretations));

        Assert.Equal(ver.InstrumentVersionId, result.InstrumentVersionId);
        Assert.Single(result.References);
        Assert.Single(result.Questions);
        Assert.Equal(4, result.Questions[0].Options.Count);
        Assert.Single(result.Interpretations);

        Assert.True(_unitOfWork.TransactionExecuted);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(_adminUserId, audit.ActorUserId);
        Assert.Equal("INSTRUMENT_VERSION", audit.TargetType);
        Assert.Equal(ver.InstrumentVersionId.ToString(), audit.TargetIdentifier);
        Assert.Contains("REFERENCES", audit.Metadata!);
        Assert.Contains("QUESTIONS", audit.Metadata!);
        Assert.Contains("ANSWER_OPTIONS", audit.Metadata!);
        Assert.Contains("SCORING", audit.Metadata!);
        Assert.Contains("INTERPRETATIONS", audit.Metadata!);
    }

    [Fact]
    public async Task ExecuteAsync_WithIdenticalPayload_ShouldBeNoOpPreservingUuidsAndNoAudit()
    {
        var (inst, ver) = SeedInstrumentAndDraftVersion();

        var existingQId = Guid.NewGuid();
        var existingOptId = Guid.NewGuid();
        var existingInterpId = Guid.NewGuid();

        _repository.DraftDefinitions[ver.InstrumentVersionId] = new DraftDefinitionDto(
            ver.InstrumentVersionId,
            inst.InstrumentId,
            1,
            "DRAFT",
            [new DraftReferenceDto(1, "Citation A", "https://example.com/a")],
            [new DraftQuestionDto(existingQId, 1, "Pregunta 1", true,
                [new DraftOptionDto(existingOptId, 1, "Opción 1", 1)])],
            [new DraftInterpretationDto(existingInterpId, "Leve", "Desc", null, 0, 5)]);

        var identicalPayload = new ReplaceDraftDefinitionCommand(
            inst.InstrumentId,
            ver.InstrumentVersionId,
            [new DraftReferenceInput(1, "Citation A", "https://example.com/a")],
            [new DraftQuestionInput(1, "Pregunta 1", true,
                [new DraftOptionInput(1, "Opción 1", 1)])],
            [new DraftInterpretationInput("Leve", "Desc", null, 0, 5)]);

        var result = await CreateUseCase().ExecuteAsync(identicalPayload);

        // UUIDs preserved from existing
        Assert.Equal(existingQId, result.Questions[0].QuestionId);
        Assert.Equal(existingOptId, result.Questions[0].Options[0].OptionId);
        Assert.Equal(existingInterpId, result.Interpretations[0].InterpretationId);

        // No transaction or audit event emitted for No-Op
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithNullContributionsInDraft_ShouldSucceed()
    {
        var (inst, ver) = SeedInstrumentAndDraftVersion();

        var questions = new[]
        {
            new DraftQuestionInput(1, "¿Pregunta cualitativa sin puntaje?", false,
            [
                new DraftOptionInput(1, "Sí", null),
                new DraftOptionInput(2, "No", null)
            ])
        };

        var result = await CreateUseCase().ExecuteAsync(new ReplaceDraftDefinitionCommand(
            inst.InstrumentId, ver.InstrumentVersionId, [], questions, []));

        Assert.Single(result.Questions);
        Assert.Null(result.Questions[0].Options[0].Contribution);
        Assert.Null(result.Questions[0].Options[1].Contribution);
        Assert.True(_unitOfWork.TransactionExecuted);
    }
}
