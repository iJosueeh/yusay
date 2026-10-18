using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Queries.ListAssessments;

public interface IListAssessmentsUseCase
{
    Task<ListAssessmentsResult> ExecuteAsync(
        ListAssessmentsQuery query,
        CancellationToken cancellationToken = default);
}
