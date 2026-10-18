using System.Security.Cryptography;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;

namespace Yusay.Application.Assessment.Commands.StartAssessmentAttempt;

public sealed class StartAssessmentAttemptUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IAssessmentAttemptRepository assessmentAttemptRepository,
    TimeProvider timeProvider) : IStartAssessmentAttemptUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IAssessmentAttemptRepository _assessmentAttemptRepository = assessmentAttemptRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<StartAssessmentAttemptResult> ExecuteAsync(
        StartAssessmentAttemptCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para iniciar una evaluación.");
        }

        if (command.InstrumentId == Guid.Empty)
        {
            throw new ValidationException("El identificador del instrumento es obligatorio.");
        }

        long lockKey = ComputeAdvisoryLockKey(userId.Value, command.InstrumentId);

        StartAssessmentAttemptResult? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            // 1. Serialización mediante advisory lock de 64 bits derivado de userId e instrumentId
            await _assessmentAttemptRepository.AcquireAttemptAdvisoryLockAsync(lockKey, tx, cancellationToken);

            // 2. Bloquear la versión PUBLISHED mediante SELECT ... FOR SHARE, coordinado con C3
            var publishedVersion = await _assessmentAttemptRepository.GetPublishedVersionForShareAsync(
                command.InstrumentId, tx, cancellationToken);

            if (publishedVersion is null)
            {
                throw new NotFoundException(
                    "El instrumento solicitado no existe o no tiene una versión publicada.");
            }

            var currentTime = _timeProvider.GetUtcNow();

            // 3. Inspección de intento previo en estado IN_PROGRESS
            var existingAttempt = await _assessmentAttemptRepository.GetActiveAttemptForUpdateAsync(
                userId.Value, command.InstrumentId, tx, cancellationToken);

            if (existingAttempt is not null)
            {
                if (currentTime < existingAttempt.ExpiresAt)
                {
                    // Intento vigente: conflicto
                    throw new ConflictException(IAssessmentAttemptRepository.ActiveAttemptConflictMessage);
                }

                // Intento vencido: Lazy Expiration (eliminar respuestas, marcar EXPIRED y fijar ended_at = expires_at)
                await _assessmentAttemptRepository.ExpireAttemptAsync(
                    existingAttempt.AttemptId,
                    existingAttempt.ExpiresAt,
                    tx,
                    cancellationToken);
            }

            // 4. Crear el nuevo intento con una única marca temporal
            var attempt = AssessmentAttempt.Create(
                userId.Value,
                command.InstrumentId,
                publishedVersion.InstrumentVersionId,
                currentTime);

            await _assessmentAttemptRepository.AddAttemptAsync(attempt, tx, cancellationToken);

            // 5. Recuperar preguntas y opciones ordenadas
            var questions = await _assessmentAttemptRepository.GetAttemptQuestionsAsync(
                publishedVersion.InstrumentVersionId,
                tx,
                cancellationToken);

            result = new StartAssessmentAttemptResult(
                attempt.AttemptId,
                attempt.InstrumentId,
                attempt.InstrumentVersionId,
                publishedVersion.Version,
                attempt.Status.ToDbValue(),
                attempt.StartedAt,
                attempt.ExpiresAt,
                questions);
        });

        return result!;
    }

    public static long ComputeAdvisoryLockKey(Guid userId, Guid instrumentId)
    {
        byte[] bytes = new byte[32];
        Buffer.BlockCopy(userId.ToByteArray(), 0, bytes, 0, 16);
        Buffer.BlockCopy(instrumentId.ToByteArray(), 0, bytes, 16, 16);
        byte[] hash = SHA256.HashData(bytes);
        return BitConverter.ToInt64(hash, 0);
    }
}
