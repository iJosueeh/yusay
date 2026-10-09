using System.Data.Common;

namespace Yusay.Application.Tracking.Repositories;

public interface IDimensionVersionRepository
{
    Task<ActiveDimensionScale?> GetActiveScaleAsync(Guid dimensionId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);

    Task<bool> DimensionExistsAsync(Guid dimensionId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
