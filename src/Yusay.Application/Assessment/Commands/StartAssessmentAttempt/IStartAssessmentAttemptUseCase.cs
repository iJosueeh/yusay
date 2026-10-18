using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.StartAssessmentAttempt;

public interface IStartAssessmentAttemptUseCase
{
    Task<StartAssessmentAttemptResult> ExecuteAsync(
        StartAssessmentAttemptCommand command,
        CancellationToken cancellationToken = default);
}
