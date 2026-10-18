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

    Task<AttemptDetailsHeaderDto?> GetAttemptHeaderForUpdateAsync(
        Guid attemptId,
        Guid userId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<AttemptDetailsHeaderDto?> GetAttemptHeaderAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, HashSet<Guid>>> GetValidQuestionsAndOptionsAsync(
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task SaveAnswersAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IReadOnlyList<AssessmentAnswerItemDto> answers,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttemptQuestionProgressDto>> GetAttemptProgressQuestionsAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task ExpireAttemptCommittedAsync(
        Guid attemptId,
        DateTimeOffset endedAt,
        CancellationToken cancellationToken = default);

    Task<SubmitAssessmentAttemptResult?> GetExistingOfficialResultAsync(
        Guid attemptId,
        Guid userId,
        IDbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<AssessmentSubmissionDataDto?> GetSubmissionDataAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task SubmitAttemptAndSaveResultAsync(
        Guid attemptId,
        Guid instrumentVersionId,
        int score,
        Guid interpretationId,
        DateTimeOffset submittedAt,
        IDbTransaction transaction,
        CancellationToken cancellationToken = default);
}
