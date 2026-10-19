using Yusay.Application.Trends.Repositories;

namespace Yusay.Domain.UnitTests.Trends.Fakes;

public sealed class FakeTrendsRepository : ITrendsRepository
{
    public Dictionary<Guid, ConceptHeaderDto> DimensionHeaders { get; } = new();
    public Dictionary<Guid, List<ConceptVersionHeaderDto>> DimensionVersions { get; } = new();
    public Dictionary<Guid, List<CompatibilityPairDto>> DimensionCompatibilityPairs { get; } = new();
    public List<(Guid UserId, Guid DimensionId, DimensionMeasurementPointDto Point)> DimensionMeasurements { get; } = new();

    public Dictionary<Guid, ConceptHeaderDto> InstrumentHeaders { get; } = new();
    public Dictionary<Guid, List<ConceptVersionHeaderDto>> InstrumentVersions { get; } = new();
    public Dictionary<Guid, List<CompatibilityPairDto>> InstrumentCompatibilityPairs { get; } = new();
    public List<(Guid UserId, Guid InstrumentId, InstrumentAssessmentPointDto Point)> InstrumentAssessments { get; } = new();

    public Task<ConceptHeaderDto?> GetDimensionHeaderAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default)
    {
        DimensionHeaders.TryGetValue(dimensionId, out var header);
        return Task.FromResult(header);
    }

    public Task<IReadOnlyList<ConceptVersionHeaderDto>> GetDimensionVersionsAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default)
    {
        if (DimensionVersions.TryGetValue(dimensionId, out var list))
        {
            return Task.FromResult<IReadOnlyList<ConceptVersionHeaderDto>>(list);
        }

        return Task.FromResult<IReadOnlyList<ConceptVersionHeaderDto>>(Array.Empty<ConceptVersionHeaderDto>());
    }

    public Task<IReadOnlyList<CompatibilityPairDto>> GetDimensionCompatibilityPairsAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default)
    {
        if (DimensionCompatibilityPairs.TryGetValue(dimensionId, out var list))
        {
            return Task.FromResult<IReadOnlyList<CompatibilityPairDto>>(list);
        }

        return Task.FromResult<IReadOnlyList<CompatibilityPairDto>>(Array.Empty<CompatibilityPairDto>());
    }

    public Task<IReadOnlyList<DimensionMeasurementPointDto>> GetDimensionMeasurementsAsync(
        Guid userId,
        Guid dimensionId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var filtered = DimensionMeasurements
            .Where(x => x.UserId == userId && x.DimensionId == dimensionId)
            .Select(x => x.Point);

        if (from.HasValue)
        {
            filtered = filtered.Where(p => p.RecordedAt >= from.Value);
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(p => p.RecordedAt <= to.Value);
        }

        var sorted = filtered
            .OrderBy(p => p.RecordedAt)
            .ThenBy(p => p.CheckInId)
            .ToList();

        return Task.FromResult<IReadOnlyList<DimensionMeasurementPointDto>>(sorted);
    }

    public Task<ConceptHeaderDto?> GetInstrumentHeaderAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        InstrumentHeaders.TryGetValue(instrumentId, out var header);
        return Task.FromResult(header);
    }

    public Task<IReadOnlyList<ConceptVersionHeaderDto>> GetInstrumentVersionsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        if (InstrumentVersions.TryGetValue(instrumentId, out var list))
        {
            return Task.FromResult<IReadOnlyList<ConceptVersionHeaderDto>>(list);
        }

        return Task.FromResult<IReadOnlyList<ConceptVersionHeaderDto>>(Array.Empty<ConceptVersionHeaderDto>());
    }

    public Task<IReadOnlyList<CompatibilityPairDto>> GetInstrumentCompatibilityPairsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        if (InstrumentCompatibilityPairs.TryGetValue(instrumentId, out var list))
        {
            return Task.FromResult<IReadOnlyList<CompatibilityPairDto>>(list);
        }

        return Task.FromResult<IReadOnlyList<CompatibilityPairDto>>(Array.Empty<CompatibilityPairDto>());
    }

    public Task<IReadOnlyList<InstrumentAssessmentPointDto>> GetInstrumentAssessmentResultsAsync(
        Guid userId,
        Guid instrumentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var filtered = InstrumentAssessments
            .Where(x => x.UserId == userId && x.InstrumentId == instrumentId)
            .Select(x => x.Point);

        if (from.HasValue)
        {
            filtered = filtered.Where(p => p.CalculatedAt >= from.Value);
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(p => p.CalculatedAt <= to.Value);
        }

        var sorted = filtered
            .OrderBy(p => p.CalculatedAt)
            .ThenBy(p => p.AttemptId)
            .ToList();

        return Task.FromResult<IReadOnlyList<InstrumentAssessmentPointDto>>(sorted);
    }
}
