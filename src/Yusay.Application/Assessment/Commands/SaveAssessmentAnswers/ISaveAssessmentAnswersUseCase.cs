using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.SaveAssessmentAnswers;

public interface ISaveAssessmentAnswersUseCase
{
    Task<SaveAssessmentAnswersResult> ExecuteAsync(
        SaveAssessmentAnswersCommand command,
        CancellationToken cancellationToken = default);
}
