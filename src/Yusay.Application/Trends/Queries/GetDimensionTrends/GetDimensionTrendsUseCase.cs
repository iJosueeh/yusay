using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Trends.Common;
using Yusay.Application.Trends.Repositories;

namespace Yusay.Application.Trends.Queries.GetDimensionTrends;

public sealed class GetDimensionTrendsUseCase(
    ICurrentUser currentUser,
    ITrendsRepository trendsRepository) : IGetDimensionTrendsUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ITrendsRepository _trendsRepository = trendsRepository;

    public async Task<DimensionTrendResult> ExecuteAsync(
        GetDimensionTrendsQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue || userId.Value == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para consultar tendencias.");
        }

        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
        {
            throw new ValidationException("El parámetro 'from' no puede ser posterior al parámetro 'to'.");
        }

        var header = await _trendsRepository.GetDimensionHeaderAsync(query.DimensionId, cancellationToken);
        if (header is null)
        {
            throw new NotFoundException("La Dimensión solicitada no existe.");
        }

        var measurements = await _trendsRepository.GetDimensionMeasurementsAsync(
            userId.Value,
            query.DimensionId,
            query.From,
            query.To,
            cancellationToken);

        if (measurements.Count == 0)
        {
            return new DimensionTrendResult(
                header.Id,
                header.Code,
                header.Name,
                Array.Empty<DimensionTrendSeriesDto>());
        }

        var versions = await _trendsRepository.GetDimensionVersionsAsync(query.DimensionId, cancellationToken);
        var compatibilityPairs = await _trendsRepository.GetDimensionCompatibilityPairsAsync(query.DimensionId, cancellationToken);

        var edges = new HashSet<(Guid, Guid)>();
        foreach (var pair in compatibilityPairs)
        {
            edges.Add((pair.VersionAId, pair.VersionBId));
            edges.Add((pair.VersionBId, pair.VersionAId));
        }

        var cliques = MaximalCliqueFinder.FindMaximalCliques(versions, v => v.VersionId, edges);

        var seriesList = new List<DimensionTrendSeriesDto>();

        foreach (var clique in cliques)
        {
            var versionIdsInClique = clique.Select(v => v.VersionId).ToHashSet();
            var matchingPoints = measurements
                .Where(m => versionIdsInClique.Contains(m.DimensionVersionId))
                .ToList();

            if (matchingPoints.Count == 0)
            {
                continue;
            }

            var refVersion = clique
                .OrderByDescending(v => v.VersionNumber)
                .ThenByDescending(v => v.VersionId)
                .First();

            var seriesVersions = clique
                .OrderBy(v => v.VersionNumber)
                .ThenBy(v => v.VersionId)
                .Select(v => new TrendVersionDto(v.VersionId, v.VersionNumber))
                .ToList();

            var sortedPoints = matchingPoints
                .OrderBy(p => p.RecordedAt)
                .ThenBy(p => p.CheckInId)
                .Select(p => new DimensionTrendPointDto(
                    p.CheckInId,
                    p.DimensionVersionId,
                    p.VersionNumber,
                    p.Value,
                    p.RecordedAt))
                .ToList();

            int count = sortedPoints.Count;
            int min = sortedPoints.Min(p => p.Value);
            int max = sortedPoints.Max(p => p.Value);
            int firstValue = sortedPoints[0].Value;
            int lastValue = sortedPoints[^1].Value;

            var sortedValues = sortedPoints.Select(p => p.Value).OrderBy(v => v).ToList();
            int medianIndex = (count - 1) / 2;
            int median = sortedValues[medianIndex];

            var frequencies = sortedPoints
                .GroupBy(p => p.Value)
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.Count());

            var summary = new DimensionTrendSummaryDto(
                count,
                min,
                max,
                median,
                firstValue,
                lastValue,
                frequencies);

            seriesList.Add(new DimensionTrendSeriesDto(
                refVersion.VersionId,
                refVersion.VersionNumber,
                seriesVersions,
                sortedPoints,
                summary));
        }

        var orderedSeries = seriesList
            .OrderBy(s => s.ReferenceVersionNumber)
            .ThenBy(s => s.ReferenceVersionId)
            .ToList();

        return new DimensionTrendResult(
            header.Id,
            header.Code,
            header.Name,
            orderedSeries);
    }
}
