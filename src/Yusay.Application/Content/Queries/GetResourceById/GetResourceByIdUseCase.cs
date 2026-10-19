using Yusay.Application.Common.Exceptions;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;

namespace Yusay.Application.Content.Queries.GetResourceById;

public sealed class GetResourceByIdUseCase(IResourceAdministrationRepository resourceRepository) : IGetResourceByIdUseCase
{
    private readonly IResourceAdministrationRepository _resourceRepository = resourceRepository;

    public async Task<ResourceDetailsDto> ExecuteAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var details = await _resourceRepository.GetDetailsByIdAsync(resourceId, cancellationToken) ?? throw new NotFoundException("El recurso especificado no existe.");

        return details;
    }
}
