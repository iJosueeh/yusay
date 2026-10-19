namespace Yusay.Application.Trends.Queries.GetInstrumentTrends;

public sealed record GetInstrumentTrendsQuery(
    Guid InstrumentId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
