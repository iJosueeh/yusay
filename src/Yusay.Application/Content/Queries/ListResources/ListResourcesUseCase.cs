using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;

namespace Yusay.Application.Content.Queries.ListResources;

public sealed class ListResourcesUseCase(IResourceAdministrationRepository resourceRepository) : IListResourcesUseCase
{
    private readonly IResourceAdministrationRepository _resourceRepository = resourceRepository;

    public async Task<IReadOnlyList<ResourceSummaryDto>> ExecuteAsync(
        ListResourcesQuery query,
        CancellationToken cancellationToken = default)
    {
        return await _resourceRepository.ListAsync(
            query.Status,
            query.Type,
            query.TopicId,
            cancellationToken);
    }
}
