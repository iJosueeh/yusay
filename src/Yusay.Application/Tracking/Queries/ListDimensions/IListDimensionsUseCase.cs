using Yusay.Application.Tracking.Queries.Dimensions;

namespace Yusay.Application.Tracking.Queries.ListDimensions;

public interface IListDimensionsUseCase
{
    Task<IReadOnlyList<DimensionCatalogResult>> ExecuteAsync(CancellationToken cancellationToken = default);
}
