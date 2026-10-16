using Yusay.Application.Tracking.Queries.Dimensions;

namespace Yusay.Application.Tracking.Queries.GetDimensionById;

public interface IGetDimensionByIdUseCase
{
    Task<DimensionCatalogResult> ExecuteAsync(
        GetDimensionByIdQuery query,
        CancellationToken cancellationToken = default);
}
