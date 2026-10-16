using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.Dimensions;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Application.Tracking.Queries.ListDimensions;

public sealed class ListDimensionsUseCase(
    ICurrentUser currentUser,
    IDimensionCatalogRepository dimensionCatalogRepository) : IListDimensionsUseCase
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
                "Se requiere una identidad de usuario autenticada para consultar el catálogo de Dimensions.");
        }

        var entries = await _dimensionCatalogRepository.ListPublishedAsync(cancellationToken);

        return DimensionCatalogResultMapper.ToResults(entries);
    }
}
