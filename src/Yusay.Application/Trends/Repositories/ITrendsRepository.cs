namespace Yusay.Application.Trends.Repositories;

public sealed record ConceptHeaderDto(Guid Id, string Code, string Name);

public sealed record ConceptVersionHeaderDto(Guid VersionId, int VersionNumber);

public sealed record CompatibilityPairDto(Guid VersionAId, Guid VersionBId);

public sealed record DimensionMeasurementPointDto(
    Guid CheckInId,
    Guid DimensionVersionId,
    int VersionNumber,
    int Value,
    DateTimeOffset RecordedAt);

public sealed record InstrumentAssessmentPointDto(
    Guid AttemptId,
    Guid InstrumentVersionId,
    int VersionNumber,
    int Score,
    DateTimeOffset CalculatedAt,
    Guid InterpretationId,
    string InterpretationLabel);

public interface ITrendsRepository
{
    Task<ConceptHeaderDto?> GetDimensionHeaderAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConceptVersionHeaderDto>> GetDimensionVersionsAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompatibilityPairDto>> GetDimensionCompatibilityPairsAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DimensionMeasurementPointDto>> GetDimensionMeasurementsAsync(
        Guid userId,
        Guid dimensionId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);

    Task<ConceptHeaderDto?> GetInstrumentHeaderAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConceptVersionHeaderDto>> GetInstrumentVersionsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CompatibilityPairDto>> GetInstrumentCompatibilityPairsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InstrumentAssessmentPointDto>> GetInstrumentAssessmentResultsAsync(
        Guid userId,
        Guid instrumentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default);
}
