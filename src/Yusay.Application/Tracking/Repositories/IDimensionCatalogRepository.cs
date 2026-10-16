namespace Yusay.Application.Tracking.Repositories;

public interface IDimensionCatalogRepository
{
    Task<IReadOnlyList<DimensionWithVersions>> ListPublishedAsync(
        CancellationToken cancellationToken = default);

    Task<DimensionWithVersions?> GetWithHistoryAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DimensionWithVersions>> ListCompleteAsync(
        CancellationToken cancellationToken = default);
}
