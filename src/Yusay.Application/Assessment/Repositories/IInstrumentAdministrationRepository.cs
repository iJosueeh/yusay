using System.Data;
using Yusay.Application.Assessment.Dtos;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;

namespace Yusay.Application.Assessment.Repositories;

public interface IInstrumentAdministrationRepository
{
    const string DuplicateCodeConflictMessage = "Ya existe un instrumento con el código especificado.";
    const string DuplicateVersionConflictMessage = "Ya existe una versión con el número especificado para este instrumento.";
    const string SinglePublishedVersionConflictMessage = "Ya existe una versión publicada para este instrumento. Debe retirar la versión publicada existente antes de publicar una nueva.";

    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);

    Task<Instrument?> GetByIdAsync(Guid instrumentId, CancellationToken cancellationToken = default);

    Task<Instrument?> GetByIdForUpdateAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task AddInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpdateInstrumentAsync(
        Instrument instrument,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<bool> VersionNumberExistsAsync(
        Guid instrumentId,
        int version,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> GetNextVersionNumberAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task AddVersionAsync(
        InstrumentVersion version,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<InstrumentVersion?> GetVersionByIdAsync(
        Guid instrumentVersionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstrumentSummaryDto>> ListAdministrativeInstrumentsAsync(
        CancellationToken cancellationToken = default);

    Task<InstrumentDetailsDto?> GetAdministrativeInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);

    Task<InstrumentVersion?> GetVersionByIdForUpdateAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<DraftDefinitionDto?> GetDraftDefinitionAsync(
        Guid instrumentVersionId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task ReplaceDraftDefinitionAsync(
        Guid instrumentVersionId,
        IReadOnlyList<InstrumentVersionReference> references,
        IReadOnlyList<(Question Question, IReadOnlyList<(AnswerOption Option, int? Contribution)> Options)> questionsWithChildren,
        IReadOnlyList<Interpretation> interpretations,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpdateVersionStatusAsync(
        Guid instrumentVersionId,
        InstrumentVersionStatus status,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
