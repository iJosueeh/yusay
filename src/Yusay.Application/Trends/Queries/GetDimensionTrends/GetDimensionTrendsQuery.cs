namespace Yusay.Application.Trends.Queries.GetDimensionTrends;

public sealed record GetDimensionTrendsQuery(
    Guid DimensionId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null);
