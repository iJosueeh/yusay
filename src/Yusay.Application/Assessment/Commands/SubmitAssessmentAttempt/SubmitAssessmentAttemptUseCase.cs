using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Commands.SubmitAssessmentAttempt;

public sealed class SubmitAssessmentAttemptUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IAssessmentAttemptRepository assessmentAttemptRepository,
    TimeProvider timeProvider) : ISubmitAssessmentAttemptUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAssessmentAttemptRepository _assessmentAttemptRepository = assessmentAttemptRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SubmitAssessmentAttemptResult> ExecuteAsync(
        SubmitAssessmentAttemptCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para enviar una evaluación.");
        }

        if (command.AttemptId == Guid.Empty)
        {
            throw new ValidationException("El identificador del intento de evaluación es obligatorio.");
        }

        bool notFound = false;
        bool isAlreadySubmitted = false;
        bool isTerminal = false;
        bool isExpired = false;
        SubmitAssessmentAttemptResult? existingResult = null;
        SubmitAssessmentAttemptResult? newResult = null;

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var attempt = await _assessmentAttemptRepository.GetAttemptHeaderForUpdateAsync(
                command.AttemptId,
                userId.Value,
                tx,
                cancellationToken);

            if (attempt is null)
            {
                notFound = true;
                return;
            }

            if (attempt.Status == "SUBMITTED")
            {
                existingResult = await _assessmentAttemptRepository.GetExistingOfficialResultAsync(
                    command.AttemptId,
                    userId.Value,
                    tx,
                    cancellationToken);

                if (existingResult is null)
                {
                    throw new InvalidOperationException(
                        "El intento se encuentra en estado SUBMITTED pero carece de resultado oficial.");
                }

                isAlreadySubmitted = true;
                return;
            }

            if (attempt.Status != "IN_PROGRESS")
            {
                isTerminal = true;
                return;
            }

            var submittedAt = _timeProvider.GetUtcNow();

            if (submittedAt >= attempt.ExpiresAt)
            {
                // Lazy Expiration: eliminar respuestas, marcar EXPIRED y fijar ended_at = expires_at.
                await _assessmentAttemptRepository.ExpireAttemptAsync(
                    attempt.AttemptId,
                    attempt.ExpiresAt,
                    tx,
                    cancellationToken);

                isExpired = true;
                return;
            }

            var subData = await _assessmentAttemptRepository.GetSubmissionDataAsync(
                attempt.AttemptId,
                attempt.InstrumentVersionId,
                tx,
                cancellationToken);

            if (subData is null)
            {
                throw new InvalidOperationException("No se pudieron cargar los datos de evaluación del intento.");
            }

            var answeredQuestionIds = subData.UserAnswers.Select(a => a.QuestionId).ToHashSet();
            var missingRequiredQuestions = subData.RequiredQuestionIds
                .Where(reqId => !answeredQuestionIds.Contains(reqId))
                .ToList();

            if (missingRequiredQuestions.Count > 0)
            {
                throw new ValidationException(
                    "No se puede enviar la evaluación: faltan respuestas a preguntas obligatorias.");
            }

            // Validar correspondencia exacta entre respuestas y scoring_contribution
            int totalScore = 0;
            foreach (var answer in subData.UserAnswers)
            {
                if (!subData.Contributions.TryGetValue((answer.QuestionId, answer.OptionId), out int contribution))
                {
                    throw new ValidationException(
                        "No se puede enviar la evaluación: una o más opciones seleccionadas carecen de contribución de scoring.");
                }

                totalScore += contribution;
            }

            // Exigir exactamente una interpretación de la versión histórica para el score obtenido
            var matchingInterpretations = subData.Interpretations
                .Where(i => totalScore >= i.LowerBound && totalScore <= i.UpperBound)
                .ToList();

            if (matchingInterpretations.Count != 1)
            {
                throw new InvalidOperationException(
                    $"No se encontró una interpretación unívoca para la puntuación {totalScore}. Coincidencias encontradas: {matchingInterpretations.Count}.");
            }

            var interpretation = matchingInterpretations[0];

            // Persistir resultado oficial y transicionar intento a SUBMITTED
            await _assessmentAttemptRepository.SubmitAttemptAndSaveResultAsync(
                attempt.AttemptId,
                attempt.InstrumentVersionId,
                totalScore,
                interpretation.InterpretationId,
                submittedAt,
                tx,
                cancellationToken);

            newResult = new SubmitAssessmentAttemptResult(
                attempt.AttemptId,
                attempt.InstrumentId,
                attempt.InstrumentVersionId,
                attempt.Version,
                "SUBMITTED",
                attempt.StartedAt,
                submittedAt,
                totalScore,
                new AssessmentInterpretationResultDto(
                    interpretation.InterpretationId,
                    interpretation.Label,
                    interpretation.Description,
                    interpretation.Limitations));
        });

        if (notFound)
        {
            throw new NotFoundException("El intento de evaluación solicitado no existe.");
        }

        if (isExpired)
        {
            throw new ConflictException("El intento de evaluación ha expirado y no admite más respuestas.");
        }

        if (isTerminal)
        {
            throw new ConflictException("El intento de evaluación ya no se encuentra en curso.");
        }

        if (isAlreadySubmitted)
        {
            return existingResult!;
        }

        return newResult!;
    }
}
