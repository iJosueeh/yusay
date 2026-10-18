using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Queries.GetAssessmentAttempt;

public interface IGetAssessmentAttemptUseCase
{
    Task<AssessmentAttemptProgressResult> ExecuteAsync(
        GetAssessmentAttemptQuery query,
        CancellationToken cancellationToken = default);
}
