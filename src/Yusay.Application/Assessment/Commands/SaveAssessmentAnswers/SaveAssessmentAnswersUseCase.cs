using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Commands.SaveAssessmentAnswers;

public sealed class SaveAssessmentAnswersUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IAssessmentAttemptRepository assessmentAttemptRepository,
    TimeProvider timeProvider) : ISaveAssessmentAnswersUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAssessmentAttemptRepository _assessmentAttemptRepository = assessmentAttemptRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<SaveAssessmentAnswersResult> ExecuteAsync(
        SaveAssessmentAnswersCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para responder una evaluación.");
        }

        if (command.AttemptId == Guid.Empty)
        {
            throw new ValidationException("El identificador del intento de evaluación es obligatorio.");
        }

        if (command.Answers is null || command.Answers.Count == 0)
        {
            throw new ValidationException("Debe proporcionar al menos una respuesta para guardar.");
        }

        if (command.Answers.Any(a => a.QuestionId == Guid.Empty || a.OptionId == Guid.Empty))
        {
            throw new ValidationException(
                "Cada respuesta debe contener identificadores válidos no vacíos para pregunta y opción.");
        }

        var hasDuplicates = command.Answers
            .GroupBy(a => a.QuestionId)
            .Any(g => g.Count() > 1);

        if (hasDuplicates)
        {
            throw new ValidationException(
                "No se permiten múltiples respuestas para la misma pregunta en una misma solicitud.");
        }

        bool notFound = false;
        bool isTerminal = false;
        bool isExpired = false;

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

            var currentTime = _timeProvider.GetUtcNow();

            if (attempt.Status != "IN_PROGRESS")
            {
                isTerminal = true;
                return;
            }

            if (currentTime >= attempt.ExpiresAt)
            {
                // Lazy Expiration: eliminar respuestas, marcar EXPIRED y fijar ended_at = expires_at.
                // Esta transacción se completa y confirma (COMMIT) antes de devolver 409 al cliente.
                await _assessmentAttemptRepository.ExpireAttemptAsync(
                    attempt.AttemptId,
                    attempt.ExpiresAt,
                    tx,
                    cancellationToken);

                isExpired = true;
                return;
            }

            // Validar que cada pregunta y opción pertenezcan a la versión histórica del intento
            var validQuestionsAndOptions = await _assessmentAttemptRepository.GetValidQuestionsAndOptionsAsync(
                attempt.InstrumentVersionId,
                tx,
                cancellationToken);

            foreach (var answer in command.Answers)
            {
                if (!validQuestionsAndOptions.TryGetValue(answer.QuestionId, out var allowedOptions))
                {
                    throw new ValidationException(
                        "La pregunta especificada no pertenece a la versión del instrumento en evaluación.");
                }

                if (!allowedOptions.Contains(answer.OptionId))
                {
                    throw new ValidationException(
                        "La opción seleccionada no es válida para la pregunta indicada.");
                }
            }

            // Guardar o actualizar (UPSERT)
            await _assessmentAttemptRepository.SaveAnswersAsync(
                command.AttemptId,
                attempt.InstrumentVersionId,
                command.Answers,
                tx,
                cancellationToken);
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

        return new SaveAssessmentAnswersResult(command.AttemptId, command.Answers.Count);
    }
}
