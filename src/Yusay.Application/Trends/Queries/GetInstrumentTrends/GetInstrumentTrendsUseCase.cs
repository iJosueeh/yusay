using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Trends.Common;
using Yusay.Application.Trends.Repositories;

namespace Yusay.Application.Trends.Queries.GetInstrumentTrends;

public sealed class GetInstrumentTrendsUseCase(
    ICurrentUser currentUser,
    ITrendsRepository trendsRepository) : IGetInstrumentTrendsUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ITrendsRepository _trendsRepository = trendsRepository;

    public async Task<InstrumentTrendResult> ExecuteAsync(
        GetInstrumentTrendsQuery query,
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

        var header = await _trendsRepository.GetInstrumentHeaderAsync(query.InstrumentId, cancellationToken);
        if (header is null)
        {
            throw new NotFoundException("El Instrumento solicitado no existe.");
        }

        var results = await _trendsRepository.GetInstrumentAssessmentResultsAsync(
            userId.Value,
            query.InstrumentId,
            query.From,
            query.To,
            cancellationToken);

        if (results.Count == 0)
        {
            return new InstrumentTrendResult(
                header.Id,
                header.Code,
                header.Name,
                Array.Empty<InstrumentTrendSeriesDto>());
        }

        var versions = await _trendsRepository.GetInstrumentVersionsAsync(query.InstrumentId, cancellationToken);
        var compatibilityPairs = await _trendsRepository.GetInstrumentCompatibilityPairsAsync(query.InstrumentId, cancellationToken);

        var edges = new HashSet<(Guid, Guid)>();
        foreach (var pair in compatibilityPairs)
        {
            edges.Add((pair.VersionAId, pair.VersionBId));
            edges.Add((pair.VersionBId, pair.VersionAId));
        }

        var cliques = MaximalCliqueFinder.FindMaximalCliques(versions, v => v.VersionId, edges);

        var seriesList = new List<InstrumentTrendSeriesDto>();

        foreach (var clique in cliques)
        {
            var versionIdsInClique = clique.Select(v => v.VersionId).ToHashSet();
            var matchingPoints = results
                .Where(r => versionIdsInClique.Contains(r.InstrumentVersionId))
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
                .OrderBy(p => p.CalculatedAt)
                .ThenBy(p => p.AttemptId)
                .Select(p => new InstrumentTrendPointDto(
                    p.AttemptId,
                    p.InstrumentVersionId,
                    p.VersionNumber,
                    p.Score,
                    p.CalculatedAt,
                    new TrendInterpretationDto(p.InterpretationId, p.InterpretationLabel)))
                .ToList();

            int count = sortedPoints.Count;
            int min = sortedPoints.Min(p => p.Score);
            int max = sortedPoints.Max(p => p.Score);
            int firstScore = sortedPoints[0].Score;
            int lastScore = sortedPoints[^1].Score;
            int delta = lastScore - firstScore;
            decimal average = Math.Round((decimal)sortedPoints.Average(p => p.Score), 2, MidpointRounding.AwayFromZero);

            var summary = new InstrumentTrendSummaryDto(
                count,
                min,
                max,
                average,
                delta,
                firstScore,
                lastScore);

            seriesList.Add(new InstrumentTrendSeriesDto(
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

        return new InstrumentTrendResult(
            header.Id,
            header.Code,
            header.Name,
            orderedSeries);
    }
}
