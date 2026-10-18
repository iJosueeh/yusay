using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Common;

namespace Yusay.Application.Assessment.Commands.ReplaceDraftDefinition;

public sealed class ReplaceDraftDefinitionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInstrumentAdministrationRepository instrumentAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IReplaceDraftDefinitionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<DraftDefinitionDto> ExecuteAsync(
        ReplaceDraftDefinitionCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Instrumentos.");
        }

        ValidateStructuralInput(command);

        DraftDefinitionDto? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var version = await _instrumentAdministrationRepository.GetVersionByIdForUpdateAsync(
                command.VersionId, tx, cancellationToken);

            if (version is null || version.InstrumentId != command.InstrumentId)
            {
                throw new NotFoundException("La versión del instrumento solicitada no existe.");
            }

            if (version.Status != InstrumentVersionStatus.Draft)
            {
                throw new ConflictException(
                    "La versión del instrumento solo puede editarse mientras está en estado DRAFT.");
            }

            var current = await _instrumentAdministrationRepository.GetDraftDefinitionAsync(
                command.VersionId, tx, cancellationToken);

            var changedCategories = DetermineChangedCategories(current, command);

            if (changedCategories.Count == 0 && current is not null)
            {
                result = current;
                return;
            }

            var references = command.References
                .OrderBy(r => r.ReferenceOrder)
                .Select(r => new InstrumentVersionReference(command.VersionId, r.ReferenceOrder, r.Citation, r.Url))
                .ToList();

            var questionsWithChildren = new List<(Question Question, IReadOnlyList<(AnswerOption Option, int? Contribution)> Options)>();
            var questionDtos = new List<DraftQuestionDto>();

            foreach (var qInput in command.Questions.OrderBy(q => q.Position))
            {
                var question = Question.Create(command.VersionId, qInput.Position, qInput.Prompt, qInput.Required);
                var optionsWithContrib = new List<(AnswerOption Option, int? Contribution)>();
                var optionDtos = new List<DraftOptionDto>();

                foreach (var optInput in qInput.Options.OrderBy(o => o.Position))
                {
                    var option = AnswerOption.Create(question.QuestionId, optInput.Position, optInput.Label);
                    optionsWithContrib.Add((option, optInput.Contribution));
                    optionDtos.Add(new DraftOptionDto(
                        option.OptionId,
                        option.Position,
                        option.Label,
                        optInput.Contribution));
                }

                questionsWithChildren.Add((question, optionsWithContrib));
                questionDtos.Add(new DraftQuestionDto(
                    question.QuestionId,
                    question.Position,
                    question.Prompt,
                    question.Required,
                    optionDtos));
            }

            var interpretations = new List<Interpretation>();
            var interpDtos = new List<DraftInterpretationDto>();

            foreach (var iInput in command.Interpretations.OrderBy(i => i.LowerBound).ThenBy(i => i.UpperBound))
            {
                var interpretation = Interpretation.Create(
                    command.VersionId,
                    iInput.Label,
                    iInput.Description,
                    iInput.Limitations,
                    iInput.LowerBound,
                    iInput.UpperBound);

                interpretations.Add(interpretation);
                interpDtos.Add(new DraftInterpretationDto(
                    interpretation.InterpretationId,
                    interpretation.Label,
                    interpretation.Description,
                    interpretation.Limitations,
                    interpretation.LowerBound,
                    interpretation.UpperBound));
            }

            await _instrumentAdministrationRepository.ReplaceDraftDefinitionAsync(
                command.VersionId,
                references,
                questionsWithChildren,
                interpretations,
                tx,
                cancellationToken);

            var audit = AuditEvent.CreateInstrumentVersionUpdated(
                command.VersionId,
                adminUserId.Value,
                changedCategories);

            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            var refDtos = references.Select(r => new DraftReferenceDto(r.ReferenceOrder, r.Citation, r.Url)).ToList();

            result = new DraftDefinitionDto(
                version.InstrumentVersionId,
                version.InstrumentId,
                version.Version,
                version.Status.ToDbValue(),
                refDtos,
                questionDtos,
                interpDtos);
        }, cancellationToken);

        return result!;
    }

    private static void ValidateStructuralInput(ReplaceDraftDefinitionCommand command)
    {
        var refOrders = new HashSet<int>();
        foreach (var r in command.References)
        {
            if (r.ReferenceOrder <= 0)
            {
                throw new DomainException($"El orden de referencia ({r.ReferenceOrder}) debe ser mayor a cero.");
            }
            if (!refOrders.Add(r.ReferenceOrder))
            {
                throw new DomainException($"El orden de referencia ({r.ReferenceOrder}) está duplicado.");
            }
            if (string.IsNullOrWhiteSpace(r.Citation))
            {
                throw new DomainException("La cita bibliográfica no puede estar vacía.");
            }
            if (!string.IsNullOrWhiteSpace(r.Url))
            {
                if (!Uri.TryCreate(r.Url.Trim(), UriKind.Absolute, out var uri) ||
                    (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                {
                    throw new DomainException($"La URL de la referencia ('{r.Url}') no es válida.");
                }
            }
        }

        // Preguntas
        var questionPositions = new HashSet<int>();
        foreach (var q in command.Questions)
        {
            if (q.Position <= 0)
            {
                throw new DomainException($"La posición de la pregunta ({q.Position}) debe ser mayor a cero.");
            }
            if (!questionPositions.Add(q.Position))
            {
                throw new DomainException($"La posición de la pregunta ({q.Position}) está duplicada.");
            }
            if (string.IsNullOrWhiteSpace(q.Prompt))
            {
                throw new DomainException("El enunciado de la pregunta no puede estar vacío.");
            }

            var optionPositions = new HashSet<int>();
            foreach (var opt in q.Options)
            {
                if (opt.Position <= 0)
                {
                    throw new DomainException($"La posición de la opción ({opt.Position}) debe ser mayor a cero.");
                }
                if (!optionPositions.Add(opt.Position))
                {
                    throw new DomainException($"La posición de la opción ({opt.Position}) está duplicada en la pregunta {q.Position}.");
                }
                if (string.IsNullOrWhiteSpace(opt.Label))
                {
                    throw new DomainException("La etiqueta de la opción de respuesta no puede estar vacía.");
                }
            }
        }

        // Interpretaciones
        foreach (var interp in command.Interpretations)
        {
            if (string.IsNullOrWhiteSpace(interp.Label))
            {
                throw new DomainException("La etiqueta de la interpretación no puede estar vacía.");
            }
            if (string.IsNullOrWhiteSpace(interp.Description))
            {
                throw new DomainException("La descripción de la interpretación no puede estar vacía.");
            }
            if (interp.LowerBound > interp.UpperBound)
            {
                throw new DomainException($"El límite inferior ({interp.LowerBound}) no puede ser mayor que el límite superior ({interp.UpperBound}).");
            }
        }
    }

    private static List<string> DetermineChangedCategories(
        DraftDefinitionDto? current,
        ReplaceDraftDefinitionCommand command)
    {
        var changed = new List<string>();

        if (current is null)
        {
            if (command.References.Count > 0) changed.Add("REFERENCES");
            if (command.Questions.Count > 0)
            {
                changed.Add("QUESTIONS");
                if (command.Questions.Any(q => q.Options.Count > 0))
                {
                    changed.Add("ANSWER_OPTIONS");
                }
                if (command.Questions.Any(q => q.Options.Any(o => o.Contribution.HasValue)))
                {
                    changed.Add("SCORING");
                }
            }
            if (command.Interpretations.Count > 0) changed.Add("INTERPRETATIONS");
            return changed;
        }

        var currRefs = current.References.OrderBy(r => r.ReferenceOrder).ToList();
        var newRefs = command.References.OrderBy(r => r.ReferenceOrder).ToList();
        var refsChanged = currRefs.Count != newRefs.Count;
        if (!refsChanged)
        {
            for (int i = 0; i < currRefs.Count; i++)
            {
                if (currRefs[i].ReferenceOrder != newRefs[i].ReferenceOrder ||
                    !string.Equals(currRefs[i].Citation, newRefs[i].Citation.Trim(), StringComparison.Ordinal) ||
                    !string.Equals(currRefs[i].Url, newRefs[i].Url?.Trim(), StringComparison.Ordinal))
                {
                    refsChanged = true;
                    break;
                }
            }
        }
        if (refsChanged) changed.Add("REFERENCES");

        var currQuestions = current.Questions.OrderBy(q => q.Position).ToList();
        var newQuestions = command.Questions.OrderBy(q => q.Position).ToList();

        var questionsChanged = currQuestions.Count != newQuestions.Count;
        var optionsChanged = false;
        var scoringChanged = false;

        if (!questionsChanged)
        {
            for (int i = 0; i < currQuestions.Count; i++)
            {
                var cq = currQuestions[i];
                var nq = newQuestions[i];

                if (cq.Position != nq.Position ||
                    !string.Equals(cq.Prompt, nq.Prompt.Trim(), StringComparison.Ordinal) ||
                    cq.Required != nq.Required)
                {
                    questionsChanged = true;
                }

                var cOpts = cq.Options.OrderBy(o => o.Position).ToList();
                var nOpts = nq.Options.OrderBy(o => o.Position).ToList();

                if (cOpts.Count != nOpts.Count)
                {
                    optionsChanged = true;
                    scoringChanged = true;
                }
                else
                {
                    for (int j = 0; j < cOpts.Count; j++)
                    {
                        if (cOpts[j].Position != nOpts[j].Position ||
                            !string.Equals(cOpts[j].Label, nOpts[j].Label.Trim(), StringComparison.Ordinal))
                        {
                            optionsChanged = true;
                        }

                        if (cOpts[j].Contribution != nOpts[j].Contribution)
                        {
                            scoringChanged = true;
                        }
                    }
                }
            }
        }
        else
        {
            optionsChanged = true;
            scoringChanged = true;
        }

        if (questionsChanged) changed.Add("QUESTIONS");
        if (optionsChanged) changed.Add("ANSWER_OPTIONS");
        if (scoringChanged) changed.Add("SCORING");

        var currInterps = current.Interpretations.OrderBy(i => i.LowerBound).ThenBy(i => i.UpperBound).ToList();
        var newInterps = command.Interpretations.OrderBy(i => i.LowerBound).ThenBy(i => i.UpperBound).ToList();

        var interpsChanged = currInterps.Count != newInterps.Count;
        if (!interpsChanged)
        {
            for (int i = 0; i < currInterps.Count; i++)
            {
                var ci = currInterps[i];
                var ni = newInterps[i];

                if (ci.LowerBound != ni.LowerBound ||
                    ci.UpperBound != ni.UpperBound ||
                    !string.Equals(ci.Label, ni.Label.Trim(), StringComparison.Ordinal) ||
                    !string.Equals(ci.Description, ni.Description.Trim(), StringComparison.Ordinal) ||
                    !string.Equals(ci.Limitations, ni.Limitations?.Trim(), StringComparison.Ordinal))
                {
                    interpsChanged = true;
                    break;
                }
            }
        }
        if (interpsChanged) changed.Add("INTERPRETATIONS");

        return changed;
    }
}
