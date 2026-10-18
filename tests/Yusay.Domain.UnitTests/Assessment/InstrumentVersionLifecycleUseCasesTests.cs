using Yusay.Application.Assessment.Commands.MarkInstrumentVersionReady;
using Yusay.Application.Assessment.Commands.PublishInstrumentVersion;
using Yusay.Application.Assessment.Commands.ReplaceDraftDefinition;
using Yusay.Application.Assessment.Commands.RetireInstrumentVersion;
using Yusay.Application.Assessment.Commands.ReturnInstrumentVersionToDraft;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Assessment.ValueObjects;
using Yusay.Domain.Common;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class InstrumentVersionLifecycleUseCasesTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeInstrumentAdministrationRepository _repository = new();
    private readonly FakeAuditEventRepository _auditRepo = new();

    private readonly MarkInstrumentVersionReadyUseCase _markReadyUseCase;
    private readonly ReturnInstrumentVersionToDraftUseCase _returnToDraftUseCase;
    private readonly PublishInstrumentVersionUseCase _publishUseCase;
    private readonly RetireInstrumentVersionUseCase _retireUseCase;

    public InstrumentVersionLifecycleUseCasesTests()
    {
        _currentUser.UserId = Guid.NewGuid();
        _markReadyUseCase = new MarkInstrumentVersionReadyUseCase(_currentUser, _unitOfWork, _repository, _auditRepo);
        _returnToDraftUseCase = new ReturnInstrumentVersionToDraftUseCase(_currentUser, _unitOfWork, _repository, _auditRepo);
        _publishUseCase = new PublishInstrumentVersionUseCase(_currentUser, _unitOfWork, _repository, _auditRepo);
        _retireUseCase = new RetireInstrumentVersionUseCase(_currentUser, _unitOfWork, _repository, _auditRepo);
    }

    private (Instrument Instrument, InstrumentVersion Version) SetupInstrumentAndCompleteDraft(
        InstrumentVersionStatus status = InstrumentVersionStatus.Draft)
    {
        var instrument = Instrument.Create(
            InstrumentCode.Create("PHQ-9"),
            "PHQ-9",
            "Cuestionario de salud del paciente",
            "Evaluación de depresión");
        _repository.Instruments.Add(instrument);

        var version = InstrumentVersion.Rehydrate(
            Guid.NewGuid(),
            instrument.InstrumentId,
            1,
            status,
            "Kroenke et al. 2001",
            "Población adulta",
            null,
            null,
            "No diagnóstica por sí sola");
        _repository.Versions.Add(version);

        var questions = new List<DraftQuestionDto>
        {
            new(Guid.NewGuid(), 1, "Poco interés o alegría", true, new List<DraftOptionDto>
            {
                new(Guid.NewGuid(), 1, "Nunca", 0),
                new(Guid.NewGuid(), 2, "Varios días", 1)
            })
        };
        var interps = new List<DraftInterpretationDto>
        {
            new(Guid.NewGuid(), "Mínimo", "Depresión mínima", null, 0, 0),
            new(Guid.NewGuid(), "Leve", "Depresión leve", null, 1, 1)
        };
        var draft = new DraftDefinitionDto(
            version.InstrumentVersionId,
            instrument.InstrumentId,
            1,
            status.ToDbValue(),
            Array.Empty<DraftReferenceDto>(),
            questions,
            interps);
        _repository.DraftDefinitions[version.InstrumentVersionId] = draft;

        return (instrument, version);
    }

    [Fact]
    public async Task MarkReady_WithCompleteDraft_ShouldTransitionAndEmitAudit()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Draft);

        var result = await _markReadyUseCase.ExecuteAsync(new MarkInstrumentVersionReadyCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("READY", result.Status);
        var updatedInRepo = _repository.Versions.First(v => v.InstrumentVersionId == ver.InstrumentVersionId);
        Assert.Equal(InstrumentVersionStatus.Ready, updatedInRepo.Status);

        var audit = _auditRepo.Events.Single();
        Assert.Equal("INSTRUMENT_VERSION_READY", audit.Action);
        Assert.Equal("INSTRUMENT_VERSION", audit.TargetType);
        Assert.Equal(ver.InstrumentVersionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task MarkReady_WhenAlreadyReady_ShouldReturn200WithoutDmlOrAudit()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Ready);

        var result = await _markReadyUseCase.ExecuteAsync(new MarkInstrumentVersionReadyCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("READY", result.Status);
        Assert.Empty(_auditRepo.Events);
    }

    [Fact]
    public async Task MarkReady_WhenIncompletePsychometrics_ShouldThrowDomainExceptionAndRollback()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Draft);
        // Clear interpretations to make it incomplete
        _repository.DraftDefinitions[ver.InstrumentVersionId] = new DraftDefinitionDto(
            ver.InstrumentVersionId, inst.InstrumentId, 1, "DRAFT",
            Array.Empty<DraftReferenceDto>(),
            _repository.DraftDefinitions[ver.InstrumentVersionId].Questions,
            Array.Empty<DraftInterpretationDto>());

        await Assert.ThrowsAsync<DomainException>(() =>
            _markReadyUseCase.ExecuteAsync(new MarkInstrumentVersionReadyCommand(inst.InstrumentId, ver.InstrumentVersionId)));

        Assert.Empty(_auditRepo.Events);
        Assert.Equal(InstrumentVersionStatus.Draft, _repository.Versions.First(v => v.InstrumentVersionId == ver.InstrumentVersionId).Status);
    }

    [Fact]
    public async Task ReturnToDraft_FromReady_ShouldTransitionAndEmitAudit()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Ready);

        var result = await _returnToDraftUseCase.ExecuteAsync(new ReturnInstrumentVersionToDraftCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("DRAFT", result.Status);
        Assert.Equal(InstrumentVersionStatus.Draft, _repository.Versions.First(v => v.InstrumentVersionId == ver.InstrumentVersionId).Status);

        var audit = _auditRepo.Events.Single();
        Assert.Equal("INSTRUMENT_VERSION_RETURNED_TO_DRAFT", audit.Action);
        Assert.Equal(ver.InstrumentVersionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task ReturnToDraft_WhenAlreadyDraft_ShouldBeIdempotentNoOp()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Draft);

        var result = await _returnToDraftUseCase.ExecuteAsync(new ReturnInstrumentVersionToDraftCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("DRAFT", result.Status);
        Assert.Empty(_auditRepo.Events);
    }

    [Fact]
    public async Task ReturnToDraft_FromPublished_ShouldThrowConflictException()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Published);

        var ex = Assert.ThrowsAsync<ConflictException>(() =>
            _returnToDraftUseCase.ExecuteAsync(new ReturnInstrumentVersionToDraftCommand(inst.InstrumentId, ver.InstrumentVersionId)));

        await ex;
        Assert.Empty(_auditRepo.Events);
    }

    [Fact]
    public async Task Publish_FromReady_ShouldTransitionAndEmitAudit()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Ready);

        var result = await _publishUseCase.ExecuteAsync(new PublishInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("PUBLISHED", result.Status);
        Assert.Equal(InstrumentVersionStatus.Published, _repository.Versions.First(v => v.InstrumentVersionId == ver.InstrumentVersionId).Status);

        var audit = _auditRepo.Events.Single();
        Assert.Equal("INSTRUMENT_VERSION_PUBLISHED", audit.Action);
        Assert.Equal(ver.InstrumentVersionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task Publish_WhenAlreadyPublished_ShouldBeIdempotentNoOp()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Published);

        var result = await _publishUseCase.ExecuteAsync(new PublishInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("PUBLISHED", result.Status);
        Assert.Empty(_auditRepo.Events);
    }

    [Fact]
    public async Task Publish_FromDraft_ShouldThrowConflictException()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Draft);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _publishUseCase.ExecuteAsync(new PublishInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId)));

        Assert.Empty(_auditRepo.Events);
    }

    [Fact]
    public async Task Retire_FromPublished_ShouldTransitionAndEmitAudit()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Published);

        var result = await _retireUseCase.ExecuteAsync(new RetireInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("RETIRED", result.Status);
        Assert.Equal(InstrumentVersionStatus.Retired, _repository.Versions.First(v => v.InstrumentVersionId == ver.InstrumentVersionId).Status);

        var audit = _auditRepo.Events.Single();
        Assert.Equal("INSTRUMENT_VERSION_RETIRED", audit.Action);
        Assert.Equal(ver.InstrumentVersionId.ToString(), audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task Retire_WhenAlreadyRetired_ShouldBeIdempotentNoOp()
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Retired);

        var result = await _retireUseCase.ExecuteAsync(new RetireInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId));

        Assert.Equal("RETIRED", result.Status);
        Assert.Empty(_auditRepo.Events);
    }

    [Theory]
    [InlineData(InstrumentVersionStatus.Draft)]
    [InlineData(InstrumentVersionStatus.Ready)]
    public async Task Retire_FromInvalidStatus_ShouldThrowConflictException(InstrumentVersionStatus invalidStatus)
    {
        var (inst, ver) = SetupInstrumentAndCompleteDraft(invalidStatus);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _retireUseCase.ExecuteAsync(new RetireInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId)));

        Assert.Empty(_auditRepo.Events);
    }

    [Fact]
    public async Task LifecycleUseCases_WithoutAuthentication_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;
        var (inst, ver) = SetupInstrumentAndCompleteDraft(InstrumentVersionStatus.Draft);

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _markReadyUseCase.ExecuteAsync(new MarkInstrumentVersionReadyCommand(inst.InstrumentId, ver.InstrumentVersionId)));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _returnToDraftUseCase.ExecuteAsync(new ReturnInstrumentVersionToDraftCommand(inst.InstrumentId, ver.InstrumentVersionId)));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _publishUseCase.ExecuteAsync(new PublishInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId)));
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _retireUseCase.ExecuteAsync(new RetireInstrumentVersionCommand(inst.InstrumentId, ver.InstrumentVersionId)));
    }

    [Fact]
    public async Task LifecycleUseCases_WhenNotFound_ShouldThrowNotFound()
    {
        var nonExistentInst = Guid.NewGuid();
        var nonExistentVer = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _markReadyUseCase.ExecuteAsync(new MarkInstrumentVersionReadyCommand(nonExistentInst, nonExistentVer)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _returnToDraftUseCase.ExecuteAsync(new ReturnInstrumentVersionToDraftCommand(nonExistentInst, nonExistentVer)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _publishUseCase.ExecuteAsync(new PublishInstrumentVersionCommand(nonExistentInst, nonExistentVer)));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _retireUseCase.ExecuteAsync(new RetireInstrumentVersionCommand(nonExistentInst, nonExistentVer)));
    }
}
