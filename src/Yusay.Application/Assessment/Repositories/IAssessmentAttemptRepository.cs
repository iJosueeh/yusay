using System.Data;
using Yusay.Application.Assessment.Dtos;
using Yusay.Domain.Assessment.Entities;

namespace Yusay.Application.Assessment.Repositories;

public interface IAssessmentAttemptRepository
{
    const string ActiveAttemptConflictMessage = "Ya tiene un intento de evaluación en curso para este instrumento.";

    Task AcquireAttemptAdvisoryLockAsync(
        long lockKey,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<PublishedVersionHeaderDto?> GetPublishedVersionForShareAsync(
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<ExistingAttemptHeaderDto?> GetActiveAttemptForUpdateAsync(
        Guid userId,
        Guid instrumentId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task ExpireAttemptAsync(
        Guid attemptId,
        DateTimeOffset endedAt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task AddAttemptAsync(
        AssessmentAttempt attempt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttemptQuestionDto>> GetAttemptQuestionsAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
