using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Queries.ListResources;

public interface IListResourcesUseCase
{
    Task<IReadOnlyList<ResourceSummaryDto>> ExecuteAsync(
        ListResourcesQuery query,
        CancellationToken cancellationToken = default);
}
