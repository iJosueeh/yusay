using Yusay.Application.Tracking.Queries.Dimensions;

namespace Yusay.Application.Tracking.Queries.ListAdministrativeDimensions;

public interface IListAdministrativeDimensionsUseCase
{
    Task<IReadOnlyList<DimensionCatalogResult>> ExecuteAsync(CancellationToken cancellationToken = default);
}
