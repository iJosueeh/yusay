namespace Yusay.Application.Trends.Queries.GetDimensionTrends;

public interface IGetDimensionTrendsUseCase
{
    Task<DimensionTrendResult> ExecuteAsync(
        GetDimensionTrendsQuery query,
        CancellationToken cancellationToken = default);
}
