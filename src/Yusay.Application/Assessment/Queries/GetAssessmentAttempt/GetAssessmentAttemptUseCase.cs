using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Queries.GetAssessmentAttempt;

public sealed class GetAssessmentAttemptUseCase(
    ICurrentUser currentUser,
    IAssessmentAttemptRepository assessmentAttemptRepository,
    TimeProvider timeProvider) : IGetAssessmentAttemptUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IAssessmentAttemptRepository _assessmentAttemptRepository = assessmentAttemptRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<AssessmentAttemptProgressResult> ExecuteAsync(
        GetAssessmentAttemptQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar una evaluación.");
        }

        if (query.AttemptId == Guid.Empty)
        {
            throw new ValidationException("El identificador del intento de evaluación es obligatorio.");
        }

        var attempt = await _assessmentAttemptRepository.GetAttemptHeaderAsync(
            query.AttemptId,
            userId.Value,
            cancellationToken);

        if (attempt is null)
        {
            throw new NotFoundException("El intento de evaluación solicitado no existe.");
        }

        var currentTime = _timeProvider.GetUtcNow();

        if (attempt.Status == "IN_PROGRESS" && currentTime >= attempt.ExpiresAt)
        {
            // Si el intento venció, expirar atómicamente y persistir en base de datos
            await _assessmentAttemptRepository.ExpireAttemptCommittedAsync(
                attempt.AttemptId,
                attempt.ExpiresAt,
                cancellationToken);

            attempt = attempt with { Status = "EXPIRED", EndedAt = attempt.ExpiresAt };
        }

        AssessmentResultDetailDto? officialResult = null;
        if (attempt.Status == "SUBMITTED")
        {
            officialResult = await _assessmentAttemptRepository.GetOfficialResultDetailAsync(
                attempt.AttemptId,
                userId.Value,
                cancellationToken);
        }

        var questions = await _assessmentAttemptRepository.GetAttemptProgressQuestionsAsync(
            attempt.AttemptId,
            attempt.InstrumentVersionId,
            transaction: null,
            cancellationToken: cancellationToken);

        return new AssessmentAttemptProgressResult(
            attempt.AttemptId,
            attempt.InstrumentId,
            attempt.InstrumentVersionId,
            attempt.Version,
            attempt.Status,
            attempt.StartedAt,
            attempt.ExpiresAt,
            questions,
            officialResult);
    }
}
