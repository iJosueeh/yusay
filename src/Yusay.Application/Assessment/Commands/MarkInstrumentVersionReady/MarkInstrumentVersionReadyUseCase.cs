using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Assessment.Models;
using Yusay.Domain.Assessment.Services;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Assessment.Commands.MarkInstrumentVersionReady;

public sealed class MarkInstrumentVersionReadyUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInstrumentAdministrationRepository instrumentAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IMarkInstrumentVersionReadyUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<InstrumentVersionResult> ExecuteAsync(
        MarkInstrumentVersionReadyCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Instrumentos.");
        }

        InstrumentVersionResult? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var version = await _instrumentAdministrationRepository.GetVersionByIdForUpdateAsync(
                command.VersionId, tx, cancellationToken);

            if (version is null || version.InstrumentId != command.InstrumentId)
            {
                throw new NotFoundException("La versión solicitada no existe para este Instrumento.");
            }

            if (version.Status == InstrumentVersionStatus.Ready)
            {
                result = new InstrumentVersionResult(
                    version.InstrumentVersionId,
                    version.InstrumentId,
                    version.Version,
                    version.Status.ToDbValue(),
                    version.SourceDescription,
                    version.Population,
                    version.AdministrationConditions,
                    version.LicenseInformation,
                    version.Limitations);
                return;
            }

            if (version.Status != InstrumentVersionStatus.Draft)
            {
                throw new ConflictException(
                    $"Transición de estado inválida: no se puede marcar como READY una versión en estado {version.Status.ToDbValue()}. Solo DRAFT puede pasar a READY.");
            }

            var draft = await _instrumentAdministrationRepository.GetDraftDefinitionAsync(
                command.VersionId, tx, cancellationToken) ?? throw new NotFoundException("La definición de la versión no fue encontrada.");

            var questionCompositions = draft.Questions.Select(q => new QuestionScoringComposition(
                new Question(q.QuestionId, command.VersionId, q.Position, q.Prompt, q.Required),
                q.Options.Select(o => new AnswerOptionScoringComposition(
                    new AnswerOption(o.OptionId, q.QuestionId, o.Position, o.Label),
                    o.Contribution)).ToList())).ToList();

            var interpretations = draft.Interpretations.Select(i => new Interpretation(
                i.InterpretationId,
                command.VersionId,
                i.Label,
                i.Description,
                i.Limitations,
                i.LowerBound,
                i.UpperBound)).ToList();

            InstrumentEditorialDomainService.EnsurePublishableComposition(questionCompositions, interpretations);

            version.MarkReady();

            await _instrumentAdministrationRepository.UpdateVersionStatusAsync(
                version.InstrumentVersionId,
                InstrumentVersionStatus.Ready,
                tx,
                cancellationToken);

            var audit = AuditEvent.CreateInstrumentVersionReady(
                version.InstrumentVersionId,
                adminUserId.Value);

            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            result = new InstrumentVersionResult(
                version.InstrumentVersionId,
                version.InstrumentId,
                version.Version,
                version.Status.ToDbValue(),
                version.SourceDescription,
                version.Population,
                version.AdministrationConditions,
                version.LicenseInformation,
                version.Limitations);
        }, cancellationToken);

        return result!;
    }
}
