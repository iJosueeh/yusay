namespace Yusay.Application.Trends.Queries.GetInstrumentTrends;

public interface IGetInstrumentTrendsUseCase
{
    Task<InstrumentTrendResult> ExecuteAsync(
        GetInstrumentTrendsQuery query,
        CancellationToken cancellationToken = default);
}
