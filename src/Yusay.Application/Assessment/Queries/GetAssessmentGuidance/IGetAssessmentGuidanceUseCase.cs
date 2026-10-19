using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Queries.GetAssessmentGuidance;

public interface IGetAssessmentGuidanceUseCase
{
    Task<AssessmentGuidanceDto> ExecuteAsync(
        GetAssessmentGuidanceQuery query,
        CancellationToken cancellationToken = default);
}
