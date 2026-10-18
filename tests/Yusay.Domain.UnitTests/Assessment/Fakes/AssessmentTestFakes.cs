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
