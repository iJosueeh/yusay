namespace Yusay.Application.Trends.Queries.GetInstrumentTrends;

public sealed record TrendVersionDto(
    Guid VersionId,
    int VersionNumber);

public sealed record TrendInterpretationDto(
    Guid InterpretationId,
    string Label);

public sealed record InstrumentTrendPointDto(
    Guid AttemptId,
    Guid InstrumentVersionId,
    int VersionNumber,
    int Score,
    DateTimeOffset CalculatedAt,
    TrendInterpretationDto Interpretation);

public sealed record InstrumentTrendSummaryDto(
    int Count,
    int? Min,
    int? Max,
    decimal? Average,
    int? Delta,
    int? FirstScore,
    int? LastScore);

public sealed record InstrumentTrendSeriesDto(
    Guid ReferenceVersionId,
    int ReferenceVersionNumber,
    IReadOnlyList<TrendVersionDto> Versions,
    IReadOnlyList<InstrumentTrendPointDto> Points,
    InstrumentTrendSummaryDto Summary);

public sealed record InstrumentTrendResult(
    Guid InstrumentId,
    string InstrumentCode,
    string InstrumentName,
    IReadOnlyList<InstrumentTrendSeriesDto> Series);
