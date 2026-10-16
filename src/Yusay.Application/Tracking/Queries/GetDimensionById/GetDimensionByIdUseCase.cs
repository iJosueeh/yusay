using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.Dimensions;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Application.Tracking.Queries.GetDimensionById;

public sealed class GetDimensionByIdUseCase(
    ICurrentUser currentUser,
    IDimensionCatalogRepository dimensionCatalogRepository) : IGetDimensionByIdUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDimensionCatalogRepository _dimensionCatalogRepository = dimensionCatalogRepository;

    public async Task<DimensionCatalogResult> ExecuteAsync(
        GetDimensionByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar una Dimension.");
        }

        var entry = await _dimensionCatalogRepository.GetWithHistoryAsync(
            query.DimensionId, cancellationToken);

        if (entry is null)
        {
            throw new NotFoundException("La Dimensión solicitada no existe.");
        }

        return DimensionCatalogResultMapper.ToResult(entry);
    }
}
