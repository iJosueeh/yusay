using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.SubmitAssessmentAttempt;

public interface ISubmitAssessmentAttemptUseCase
{
    Task<SubmitAssessmentAttemptResult> ExecuteAsync(
        SubmitAssessmentAttemptCommand command,
        CancellationToken cancellationToken = default);
}
