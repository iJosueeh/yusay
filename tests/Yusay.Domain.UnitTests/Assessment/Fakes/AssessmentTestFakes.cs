using System.Data;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;

namespace Yusay.Domain.UnitTests.Assessment.Fakes;

public sealed class FakeInstrumentAdministrationRepository : IInstrumentAdministrationRepository
{
    public List<Instrument> Instruments { get; } = new();
    public List<InstrumentVersion> Versions { get; } = new();

    public bool ThrowOnAddInstrument { get; set; }
    public bool ThrowOnAddVersion { get; set; }

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        var exists = Instruments.Any(i => string.Equals(i.Code.Value, code, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task<Instrument?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default)
    {
        var inst = Instruments.FirstOrDefault(i => i.InstrumentId == instrumentId);
        return Task.FromResult(inst);
    }

    public Task<Instrument?> GetByIdForUpdateAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return GetByIdAsync(instrumentId, cancellationToken);
    }

    public Task AddInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddInstrument)
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateCodeConflictMessage);
        }

        if (Instruments.Any(i => string.Equals(i.Code.Value, instrument.Code.Value, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateCodeConflictMessage);
        }

        Instruments.Add(instrument);
        return Task.CompletedTask;
    }

    public Task UpdateInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var idx = Instruments.FindIndex(i => i.InstrumentId == instrument.InstrumentId);
        if (idx >= 0)
        {
            Instruments[idx] = instrument;
        }

        return Task.CompletedTask;
    }

    public Task<bool> VersionNumberExistsAsync(
        Guid instrumentId,
        int version,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var exists = Versions.Any(v => v.InstrumentId == instrumentId && v.Version == version);
        return Task.FromResult(exists);
    }

    public Task<int> GetNextVersionNumberAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var existing = Versions.Where(v => v.InstrumentId == instrumentId).Select(v => v.Version).ToList();
        var next = existing.Count == 0 ? 1 : existing.Max() + 1;
        return Task.FromResult(next);
    }

    public Task AddVersionAsync(
        InstrumentVersion version,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddVersion)
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateVersionConflictMessage);
        }

        if (Versions.Any(v => v.InstrumentId == version.InstrumentId && v.Version == version.Version))
        {
            throw new ConflictException(IInstrumentAdministrationRepository.DuplicateVersionConflictMessage);
        }

        Versions.Add(version);
        return Task.CompletedTask;
    }

    public Task<InstrumentVersion?> GetVersionByIdAsync(
        Guid instrumentVersionId,
        CancellationToken cancellationToken = default)
    {
        var ver = Versions.FirstOrDefault(v => v.InstrumentVersionId == instrumentVersionId);
        return Task.FromResult(ver);
    }

    public Task<IReadOnlyList<InstrumentSummaryDto>> ListAdministrativeInstrumentsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = Instruments
            .OrderBy(i => i.Name)
            .Select(i =>
            {
                var instVersions = Versions.Where(v => v.InstrumentId == i.InstrumentId).ToList();
                var count = instVersions.Count;
                var latest = count == 0 ? (int?)null : instVersions.Max(v => v.Version);
                return new InstrumentSummaryDto(
                    i.InstrumentId,
                    i.Code.Value,
                    i.Name,
                    i.Description,
                    i.Purpose,
                    count,
                    latest);
            })
            .ToList();

        return Task.FromResult<IReadOnlyList<InstrumentSummaryDto>>(result);
    }

    public Task<InstrumentDetailsDto?> GetAdministrativeInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        var inst = Instruments.FirstOrDefault(i => i.InstrumentId == instrumentId);
        if (inst == null)
        {
            return Task.FromResult<InstrumentDetailsDto?>(null);
        }

        var instVersions = Versions
            .Where(v => v.InstrumentId == instrumentId)
            .OrderByDescending(v => v.Version)
            .Select(v => new InstrumentVersionSummaryDto(
                v.InstrumentVersionId,
                v.Version,
                v.Status.ToDbValue(),
                v.SourceDescription,
                v.Population,
                v.AdministrationConditions,
                v.LicenseInformation,
                v.Limitations))
            .ToList();

        return Task.FromResult<InstrumentDetailsDto?>(new InstrumentDetailsDto(
            inst.InstrumentId,
            inst.Code.Value,
            inst.Name,
            inst.Description,
            inst.Purpose,
            instVersions));
    }

    public Dictionary<Guid, DraftDefinitionDto> DraftDefinitions { get; } = new();

    public Task<InstrumentVersion?> GetVersionByIdForUpdateAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return GetVersionByIdAsync(instrumentVersionId, cancellationToken);
    }

    public Task<DraftDefinitionDto?> GetDraftDefinitionAsync(
        Guid versionId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (DraftDefinitions.TryGetValue(versionId, out var existing))
        {
            return Task.FromResult<DraftDefinitionDto?>(existing);
        }

        var version = Versions.FirstOrDefault(v => v.InstrumentVersionId == versionId);
        if (version == null)
        {
            return Task.FromResult<DraftDefinitionDto?>(null);
        }

        var empty = new DraftDefinitionDto(
            version.InstrumentVersionId,
            version.InstrumentId,
            version.Version,
            version.Status.ToDbValue(),
            Array.Empty<DraftReferenceDto>(),
            Array.Empty<DraftQuestionDto>(),
            Array.Empty<DraftInterpretationDto>());

        return Task.FromResult<DraftDefinitionDto?>(empty);
    }

    public Task ReplaceDraftDefinitionAsync(
        Guid instrumentVersionId,
        IReadOnlyList<InstrumentVersionReference> references,
        IReadOnlyList<(Question Question, IReadOnlyList<(AnswerOption Option, int? Contribution)> Options)> questionsWithChildren,
        IReadOnlyList<Interpretation> interpretations,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var version = Versions.FirstOrDefault(v => v.InstrumentVersionId == instrumentVersionId);
        var refDtos = references.Select(r => new DraftReferenceDto(r.ReferenceOrder, r.Citation, r.Url)).ToList();
        var qDtos = questionsWithChildren.Select(q => new DraftQuestionDto(
            q.Question.QuestionId,
            q.Question.Position,
            q.Question.Prompt,
            q.Question.Required,
            q.Options.Select(o => new DraftOptionDto(
                o.Option.OptionId,
                o.Option.Position,
                o.Option.Label,
                o.Contribution)).ToList())).ToList();
        var iDtos = interpretations.Select(i => new DraftInterpretationDto(
            i.InterpretationId,
            i.Label,
            i.Description,
            i.Limitations,
            i.LowerBound,
            i.UpperBound)).ToList();

        var dto = new DraftDefinitionDto(
            instrumentVersionId,
            version?.InstrumentId ?? Guid.Empty,
            version?.Version ?? 1,
            version?.Status.ToDbValue() ?? "DRAFT",
            refDtos,
            qDtos,
            iDtos);

        DraftDefinitions[instrumentVersionId] = dto;
        return Task.CompletedTask;
    }

    public Task UpdateVersionStatusAsync(
        Guid instrumentVersionId,
        InstrumentVersionStatus status,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var idx = Versions.FindIndex(v => v.InstrumentVersionId == instrumentVersionId);
        if (idx >= 0)
        {
            var old = Versions[idx];
            var updated = InstrumentVersion.Rehydrate(
                old.InstrumentVersionId,
                old.InstrumentId,
                old.Version,
                status,
                old.SourceDescription,
                old.Population,
                old.AdministrationConditions,
                old.LicenseInformation,
                old.Limitations);
            Versions[idx] = updated;

            if (DraftDefinitions.TryGetValue(instrumentVersionId, out var draft))
            {
                DraftDefinitions[instrumentVersionId] = new DraftDefinitionDto(
                    draft.InstrumentVersionId,
                    draft.InstrumentId,
                    draft.Version,
                    status.ToDbValue(),
                    draft.References,
                    draft.Questions,
                    draft.Interpretations);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class FakeInstrumentCatalogRepository : IInstrumentCatalogRepository
{
    public List<PublishedInstrumentSummaryDto> Summaries { get; } = new();
    public Dictionary<Guid, PublishedInstrumentDetailsDto> Details { get; } = new();

    public Task<IReadOnlyList<PublishedInstrumentSummaryDto>> ListPublishedInstrumentsAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<PublishedInstrumentSummaryDto>>(Summaries);
    }

    public Task<PublishedInstrumentDetailsDto?> GetPublishedInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        Details.TryGetValue(instrumentId, out var detail);
        return Task.FromResult(detail);
    }
}

public sealed class FakeAssessmentAttemptRepository : IAssessmentAttemptRepository
{
    public List<long> AcquiredLockKeys { get; } = new();
    public Dictionary<Guid, PublishedVersionHeaderDto> PublishedVersions { get; } = new();
    public List<AssessmentAttempt> Attempts { get; } = new();
    public Dictionary<Guid, List<AttemptQuestionDto>> VersionQuestions { get; } = new();
    public List<Guid> ExpiredAttempts { get; } = new();
    public bool ThrowOnAddAttempt { get; set; }

    public Task AcquireAttemptAdvisoryLockAsync(
        long lockKey,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        AcquiredLockKeys.Add(lockKey);
        return Task.CompletedTask;
    }

    public Task<PublishedVersionHeaderDto?> GetPublishedVersionForShareAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        PublishedVersions.TryGetValue(instrumentId, out var header);
        return Task.FromResult(header);
    }

    public Task<ExistingAttemptHeaderDto?> GetActiveAttemptForUpdateAsync(
        Guid userId,
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        var active = Attempts.FirstOrDefault(a =>
            a.UserId == userId &&
            a.InstrumentId == instrumentId &&
            a.Status == AssessmentAttemptStatus.InProgress);

        if (active is null)
        {
            return Task.FromResult<ExistingAttemptHeaderDto?>(null);
        }

        return Task.FromResult<ExistingAttemptHeaderDto?>(
            new ExistingAttemptHeaderDto(active.AttemptId, active.StartedAt, active.ExpiresAt));
    }

    public Task ExpireAttemptAsync(
        Guid attemptId,
        DateTimeOffset endedAt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        ExpiredAttempts.Add(attemptId);
        SavedAnswers.Remove(attemptId);
        var idx = Attempts.FindIndex(a => a.AttemptId == attemptId);
        if (idx >= 0)
        {
            Attempts[idx].Expire(endedAt);
        }
        return Task.CompletedTask;
    }

    public Task AddAttemptAsync(
        AssessmentAttempt attempt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (ThrowOnAddAttempt)
        {
            throw new ConflictException(IAssessmentAttemptRepository.ActiveAttemptConflictMessage);
        }

        if (Attempts.Any(a => a.UserId == attempt.UserId && a.InstrumentId == attempt.InstrumentId && a.Status == AssessmentAttemptStatus.InProgress))
        {
            throw new ConflictException(IAssessmentAttemptRepository.ActiveAttemptConflictMessage);
        }

        Attempts.Add(attempt);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AttemptQuestionDto>> GetAttemptQuestionsAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (VersionQuestions.TryGetValue(instrumentVersionId, out var questions))
        {
            return Task.FromResult<IReadOnlyList<AttemptQuestionDto>>(questions);
        }

        return Task.FromResult<IReadOnlyList<AttemptQuestionDto>>(Array.Empty<AttemptQuestionDto>());
    }

    public Dictionary<Guid, Dictionary<Guid, Guid>> SavedAnswers { get; } = new();
    public Dictionary<Guid, Dictionary<Guid, HashSet<Guid>>> VersionAllowedOptions { get; } = new();
    public Dictionary<Guid, int> VersionNumbers { get; } = new();

    public Task<AttemptDetailsHeaderDto?> GetAttemptHeaderForUpdateAsync(
        Guid attemptId,
        Guid userId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        return GetAttemptHeaderAsync(attemptId, userId, cancellationToken);
    }

    public Task<AttemptDetailsHeaderDto?> GetAttemptHeaderAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var attempt = Attempts.FirstOrDefault(a => a.AttemptId == attemptId && a.UserId == userId);
        if (attempt is null)
        {
            return Task.FromResult<AttemptDetailsHeaderDto?>(null);
        }

        var version = VersionNumbers.TryGetValue(attempt.InstrumentVersionId, out var ver) ? ver : 1;
        return Task.FromResult<AttemptDetailsHeaderDto?>(new AttemptDetailsHeaderDto(
            attempt.AttemptId,
            attempt.UserId,
            attempt.InstrumentId,
            attempt.InstrumentVersionId,
            version,
            attempt.Status.ToDbValue(),
            attempt.StartedAt,
            attempt.ExpiresAt,
            attempt.EndedAt));
    }

    public Task<IReadOnlyDictionary<Guid, HashSet<Guid>>> GetValidQuestionsAndOptionsAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (VersionAllowedOptions.TryGetValue(instrumentVersionId, out var dict))
        {
            return Task.FromResult<IReadOnlyDictionary<Guid, HashSet<Guid>>>(dict);
        }

        return Task.FromResult<IReadOnlyDictionary<Guid, HashSet<Guid>>>(
            new Dictionary<Guid, HashSet<Guid>>());
    }

    public Task SaveAnswersAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IReadOnlyList<AssessmentAnswerItemDto> answers,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        if (!SavedAnswers.TryGetValue(attemptId, out var attemptAnswers))
        {
            attemptAnswers = new Dictionary<Guid, Guid>();
            SavedAnswers[attemptId] = attemptAnswers;
        }

        foreach (var a in answers)
        {
            attemptAnswers[a.QuestionId] = a.OptionId;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AttemptQuestionProgressDto>> GetAttemptProgressQuestionsAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        SavedAnswers.TryGetValue(attemptId, out var attemptAnswers);

        if (!VersionQuestions.TryGetValue(instrumentVersionId, out var questions))
        {
            return Task.FromResult<IReadOnlyList<AttemptQuestionProgressDto>>(
                Array.Empty<AttemptQuestionProgressDto>());
        }

        var progressList = questions.Select(q =>
        {
            Guid? selectedOptionId = null;
            if (attemptAnswers is not null && attemptAnswers.TryGetValue(q.QuestionId, out var optId))
            {
                selectedOptionId = optId;
            }

            return new AttemptQuestionProgressDto(
                q.QuestionId,
                q.Position,
                q.Prompt,
                q.Required,
                selectedOptionId,
                q.Options);
        }).ToList();

        return Task.FromResult<IReadOnlyList<AttemptQuestionProgressDto>>(progressList);
    }

    public Task ExpireAttemptCommittedAsync(
        Guid attemptId,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken = default)
    {
        return ExpireAttemptAsync(attemptId, endedAt, null!, cancellationToken);
    }

    public Dictionary<Guid, (int Score, Guid InterpretationId, DateTimeOffset CalculatedAt)> AttemptResults { get; } = new();
    public Dictionary<Guid, List<Guid>> VersionRequiredQuestions { get; } = new();
    public Dictionary<Guid, Dictionary<(Guid QuestionId, Guid OptionId), int>> VersionContributions { get; } = new();
    public Dictionary<Guid, List<InterpretationDataDto>> VersionInterpretations { get; } = new();

    public Task<SubmitAssessmentAttemptResult?> GetExistingOfficialResultAsync(
        Guid attemptId,
        Guid userId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var attempt = Attempts.FirstOrDefault(a => a.AttemptId == attemptId && a.UserId == userId);
        if (attempt is null || !AttemptResults.TryGetValue(attemptId, out var res))
        {
            return Task.FromResult<SubmitAssessmentAttemptResult?>(null);
        }

        var version = VersionNumbers.TryGetValue(attempt.InstrumentVersionId, out var ver) ? ver : 1;
        var interp = VersionInterpretations.TryGetValue(attempt.InstrumentVersionId, out var interps)
            ? interps.FirstOrDefault(i => i.InterpretationId == res.InterpretationId)
            : null;

        var interpDto = new AssessmentInterpretationResultDto(
            interp?.InterpretationId ?? res.InterpretationId,
            interp?.Label ?? "Interpretación",
            interp?.Description ?? "Descripción",
            interp?.Limitations);

        return Task.FromResult<SubmitAssessmentAttemptResult?>(new SubmitAssessmentAttemptResult(
            attempt.AttemptId,
            attempt.InstrumentId,
            attempt.InstrumentVersionId,
            version,
            attempt.Status.ToDbValue(),
            attempt.StartedAt,
            attempt.EndedAt ?? res.CalculatedAt,
            res.Score,
            interpDto));
    }

    public Task<AssessmentSubmissionDataDto?> GetSubmissionDataAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> reqQuestions = VersionRequiredQuestions.TryGetValue(instrumentVersionId, out var req)
            ? req
            : Array.Empty<Guid>();

        IReadOnlyList<(Guid QuestionId, Guid OptionId)> userAnswers = SavedAnswers.TryGetValue(attemptId, out var answers)
            ? answers.Select(kv => (kv.Key, kv.Value)).ToList()
            : Array.Empty<(Guid QuestionId, Guid OptionId)>();

        IReadOnlyDictionary<(Guid QuestionId, Guid OptionId), int> contributions = VersionContributions.TryGetValue(instrumentVersionId, out var c)
            ? c
            : new Dictionary<(Guid QuestionId, Guid OptionId), int>();

        IReadOnlyList<InterpretationDataDto> interps = VersionInterpretations.TryGetValue(instrumentVersionId, out var i)
            ? i
            : Array.Empty<InterpretationDataDto>();

        return Task.FromResult<AssessmentSubmissionDataDto?>(new AssessmentSubmissionDataDto(
            reqQuestions,
            userAnswers,
            contributions,
            interps));
    }

    public Task SubmitAttemptAndSaveResultAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        int score,
        Guid interpretationId,
        DateTimeOffset submittedAt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        AttemptResults[attemptId] = (score, interpretationId, submittedAt);
        var idx = Attempts.FindIndex(a => a.AttemptId == attemptId);
        if (idx >= 0)
        {
            Attempts[idx].Submit(submittedAt);
        }

        return Task.CompletedTask;
    }
}
