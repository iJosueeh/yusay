using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.Dimensions;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Application.Tracking.Queries.ListAdministrativeDimensions;

public sealed class ListAdministrativeDimensionsUseCase(
    ICurrentUser currentUser,
    IDimensionCatalogRepository dimensionCatalogRepository) : IListAdministrativeDimensionsUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDimensionCatalogRepository _dimensionCatalogRepository = dimensionCatalogRepository;

    public async Task<IReadOnlyList<DimensionCatalogResult>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar el catálogo administrativo de Dimensions.");
        }

        var entries = await _dimensionCatalogRepository.ListCompleteAsync(cancellationToken);

        return DimensionCatalogResultMapper.ToResults(entries);
    }
}
