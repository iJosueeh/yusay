using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;

namespace Yusay.Application.Content.Queries.GetPublishedResourceById;

public sealed class GetPublishedResourceByIdUseCase(
    ICurrentUser currentUser,
    IResourceCatalogRepository resourceCatalogRepository) : IGetPublishedResourceByIdUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IResourceCatalogRepository _resourceCatalogRepository = resourceCatalogRepository;

    public async Task<ResourceDetailsDto> ExecuteAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad autenticada para consultar recursos educativos.");
        }

        var details = await _resourceCatalogRepository.GetPublishedByIdAsync(resourceId, cancellationToken);
        if (details is null)
        {
            throw new NotFoundException("El recurso solicitado no existe o no se encuentra disponible.");
        }

        return details;
    }
}
