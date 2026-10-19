using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Queries.ListPublishedResources;

public interface IListPublishedResourcesUseCase
{
    Task<ListResourcesResult> ExecuteAsync(
        ListPublishedResourcesQuery query,
        CancellationToken cancellationToken = default);
}
