namespace Yusay.Api.Contracts.Trends;

public sealed record TrendVersionResponse(
    Guid VersionId,
    int VersionNumber);

public sealed record DimensionTrendPointResponse(
    Guid CheckInId,
    Guid DimensionVersionId,
    int VersionNumber,
    int Value,
    DateTimeOffset RecordedAt);

public sealed record DimensionTrendSummaryResponse(
    int Count,
    int? Min,
    int? Max,
    int? Median,
    int? FirstValue,
    int? LastValue,
    IReadOnlyDictionary<int, int> Frequencies);

public sealed record DimensionTrendSeriesResponse(
    Guid ReferenceVersionId,
    int ReferenceVersionNumber,
    IReadOnlyList<TrendVersionResponse> Versions,
    IReadOnlyList<DimensionTrendPointResponse> Points,
    DimensionTrendSummaryResponse Summary);

public sealed record DimensionTrendResponse(
    Guid DimensionId,
    string DimensionCode,
    string DimensionName,
    IReadOnlyList<DimensionTrendSeriesResponse> Series);

public sealed record TrendInterpretationResponse(
    Guid InterpretationId,
    string Label);

public sealed record InstrumentTrendPointResponse(
    Guid AttemptId,
    Guid InstrumentVersionId,
    int VersionNumber,
    int Score,
    DateTimeOffset CalculatedAt,
    TrendInterpretationResponse Interpretation);

public sealed record InstrumentTrendSummaryResponse(
    int Count,
    int? Min,
    int? Max,
    decimal? Average,
    int? Delta,
    int? FirstScore,
    int? LastScore);

public sealed record InstrumentTrendSeriesResponse(
    Guid ReferenceVersionId,
    int ReferenceVersionNumber,
    IReadOnlyList<TrendVersionResponse> Versions,
    IReadOnlyList<InstrumentTrendPointResponse> Points,
    InstrumentTrendSummaryResponse Summary);

public sealed record InstrumentTrendResponse(
    Guid InstrumentId,
    string InstrumentCode,
    string InstrumentName,
    IReadOnlyList<InstrumentTrendSeriesResponse> Series);
