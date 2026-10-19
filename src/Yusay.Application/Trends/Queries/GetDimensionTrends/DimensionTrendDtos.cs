namespace Yusay.Application.Trends.Queries.GetDimensionTrends;

public sealed record TrendVersionDto(
    Guid VersionId,
    int VersionNumber);

public sealed record DimensionTrendPointDto(
    Guid CheckInId,
    Guid DimensionVersionId,
    int VersionNumber,
    int Value,
    DateTimeOffset RecordedAt);

public sealed record DimensionTrendSummaryDto(
    int Count,
    int? Min,
    int? Max,
    int? Median,
    int? FirstValue,
    int? LastValue,
    IReadOnlyDictionary<int, int> Frequencies);

public sealed record DimensionTrendSeriesDto(
    Guid ReferenceVersionId,
    int ReferenceVersionNumber,
    IReadOnlyList<TrendVersionDto> Versions,
    IReadOnlyList<DimensionTrendPointDto> Points,
    DimensionTrendSummaryDto Summary);

public sealed record DimensionTrendResult(
    Guid DimensionId,
    string DimensionCode,
    string DimensionName,
    IReadOnlyList<DimensionTrendSeriesDto> Series);
