using Yusay.Application.Common.Exceptions;
using Yusay.Application.Trends.Queries.GetDimensionTrends;
using Yusay.Application.Trends.Repositories;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Trends.Fakes;

namespace Yusay.Domain.UnitTests.Trends;

public sealed class GetDimensionTrendsUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeTrendsRepository _repo = new();
    private readonly GetDimensionTrendsUseCase _useCase;

    public GetDimensionTrendsUseCaseTests()
    {
        _useCase = new GetDimensionTrendsUseCase(_currentUser, _repo);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorizedException()
    {
        _currentUser.UserId = null;
        var query = new GetDimensionTrendsQuery(Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ExecuteAsync_WithFromLaterThanTo_ShouldThrowValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var query = new GetDimensionTrendsQuery(
            Guid.NewGuid(),
            From: now.AddDays(1),
            To: now);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _useCase.ExecuteAsync(query));

        Assert.Equal("El parámetro 'from' no puede ser posterior al parámetro 'to'.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentDimension_ShouldThrowNotFoundException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var query = new GetDimensionTrendsQuery(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _useCase.ExecuteAsync(query));

        Assert.Equal("La Dimensión solicitada no existe.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMeasurementsForUser_ShouldReturnEmptySeries()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var dimId = Guid.NewGuid();

        _repo.DimensionHeaders[dimId] = new ConceptHeaderDto(dimId, "mood", "Estado de ánimo");
        _repo.DimensionVersions[dimId] = new List<ConceptVersionHeaderDto>
        {
            new(Guid.NewGuid(), 1)
        };

        var result = await _useCase.ExecuteAsync(new GetDimensionTrendsQuery(dimId));

        Assert.Equal(dimId, result.DimensionId);
        Assert.Equal("mood", result.DimensionCode);
        Assert.Equal("Estado de ánimo", result.DimensionName);
        Assert.Empty(result.Series);
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleVersion_ShouldCalculateOrdinalStatisticsCorrectly()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var dimId = Guid.NewGuid();
        var verId = Guid.NewGuid();

        _repo.DimensionHeaders[dimId] = new ConceptHeaderDto(dimId, "energy", "Energía");
        _repo.DimensionVersions[dimId] = new List<ConceptVersionHeaderDto>
        {
            new(verId, 1)
        };

        var baseTime = DateTimeOffset.UtcNow.AddDays(-10);

        // 4 measurements (even N): values 2, 4, 1, 5
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(Guid.NewGuid(), verId, 1, 2, baseTime.AddHours(1))));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(Guid.NewGuid(), verId, 1, 4, baseTime.AddHours(2))));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(Guid.NewGuid(), verId, 1, 1, baseTime.AddHours(3))));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(Guid.NewGuid(), verId, 1, 5, baseTime.AddHours(4))));

        var result = await _useCase.ExecuteAsync(new GetDimensionTrendsQuery(dimId));

        Assert.Single(result.Series);
        var series = result.Series[0];
        Assert.Equal(verId, series.ReferenceVersionId);
        Assert.Equal(1, series.ReferenceVersionNumber);
        Assert.Equal(4, series.Points.Count);

        // Verification of ordinal summary:
        // Sorted values: [1, 2, 4, 5]. For even N=4, lower central element is index (4-1)/2 = 1 -> value 2.
        Assert.Equal(4, series.Summary.Count);
        Assert.Equal(1, series.Summary.Min);
        Assert.Equal(5, series.Summary.Max);
        Assert.Equal(2, series.Summary.FirstValue); // value at first point (baseTime + 1h)
        Assert.Equal(5, series.Summary.LastValue);  // value at last point (baseTime + 4h)
        Assert.Equal(2, series.Summary.Median);     // lower central element when N is even

        Assert.Equal(1, series.Summary.Frequencies[1]);
        Assert.Equal(1, series.Summary.Frequencies[2]);
        Assert.Equal(1, series.Summary.Frequencies[4]);
        Assert.Equal(1, series.Summary.Frequencies[5]);
    }

    [Fact]
    public async Task ExecuteAsync_WithOddCount_ShouldCalculateExactMedian()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var dimId = Guid.NewGuid();
        var verId = Guid.NewGuid();

        _repo.DimensionHeaders[dimId] = new ConceptHeaderDto(dimId, "stress", "Estrés");
        _repo.DimensionVersions[dimId] = new List<ConceptVersionHeaderDto> { new(verId, 1) };

        var now = DateTimeOffset.UtcNow;
        // 3 measurements: values 4, 1, 5 -> sorted [1, 4, 5] -> median is 4
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(Guid.NewGuid(), verId, 1, 4, now.AddHours(1))));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(Guid.NewGuid(), verId, 1, 1, now.AddHours(2))));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(Guid.NewGuid(), verId, 1, 5, now.AddHours(3))));

        var result = await _useCase.ExecuteAsync(new GetDimensionTrendsQuery(dimId));

        var series = Assert.Single(result.Series);
        Assert.Equal(3, series.Summary.Count);
        Assert.Equal(4, series.Summary.Median);
        Assert.Equal(4, series.Summary.FirstValue);
        Assert.Equal(5, series.Summary.LastValue);
    }

    [Fact]
    public async Task ExecuteAsync_WithV1V2AndV2V3Compatible_WithoutV1V3_ShouldGenerateTwoOverlappingSeries_WithoutTransitivity()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var dimId = Guid.NewGuid();

        var v1Id = Guid.NewGuid();
        var v2Id = Guid.NewGuid();
        var v3Id = Guid.NewGuid();

        _repo.DimensionHeaders[dimId] = new ConceptHeaderDto(dimId, "sleep", "Sueño");
        _repo.DimensionVersions[dimId] = new List<ConceptVersionHeaderDto>
        {
            new(v1Id, 1),
            new(v2Id, 2),
            new(v3Id, 3)
        };

        // Declaraciones explícitas: (V1, V2) y (V2, V3). No existe (V1, V3).
        _repo.DimensionCompatibilityPairs[dimId] = new List<CompatibilityPairDto>
        {
            new(v1Id, v2Id),
            new(v2Id, v3Id)
        };

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        var p1Id = Guid.NewGuid();
        var p2Id = Guid.NewGuid();
        var p3Id = Guid.NewGuid();

        // O1 en V1 (valor 2), O2 en V2 (valor 3), O3 en V3 (valor 4)
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(p1Id, v1Id, 1, 2, t1)));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(p2Id, v2Id, 2, 3, t2)));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(p3Id, v3Id, 3, 4, t3)));

        var result = await _useCase.ExecuteAsync(new GetDimensionTrendsQuery(dimId));

        // Debe producir 2 series
        Assert.Equal(2, result.Series.Count);

        // Serie 1: {V1, V2}, Referencia V2 (versión de mayor número del grupo: 2)
        var s1 = result.Series[0];
        Assert.Equal(v2Id, s1.ReferenceVersionId);
        Assert.Equal(2, s1.ReferenceVersionNumber);
        Assert.Equal(2, s1.Versions.Count);
        Assert.Equal(2, s1.Points.Count);
        Assert.Contains(s1.Points, p => p.CheckInId == p1Id && p.VersionNumber == 1);
        Assert.Contains(s1.Points, p => p.CheckInId == p2Id && p.VersionNumber == 2);
        Assert.DoesNotContain(s1.Points, p => p.CheckInId == p3Id);

        // Serie 2: {V2, V3}, Referencia V3 (versión de mayor número del grupo: 3)
        var s2 = result.Series[1];
        Assert.Equal(v3Id, s2.ReferenceVersionId);
        Assert.Equal(3, s2.ReferenceVersionNumber);
        Assert.Equal(2, s2.Versions.Count);
        Assert.Equal(2, s2.Points.Count);
        Assert.Contains(s2.Points, p => p.CheckInId == p2Id && p.VersionNumber == 2);
        Assert.Contains(s2.Points, p => p.CheckInId == p3Id && p.VersionNumber == 3);
        Assert.DoesNotContain(s2.Points, p => p.CheckInId == p1Id);

        // P2 aparece en ambas series (identificando versiones y puntos originales)
        // V1 y V3 NUNCA aparecen juntas en ninguna serie (No transitividad)
        Assert.DoesNotContain(result.Series, s =>
            s.Points.Any(p => p.VersionNumber == 1) && s.Points.Any(p => p.VersionNumber == 3));
    }

    [Fact]
    public async Task ExecuteAsync_WithTieBreak_ShouldSortByOccurredAtAscThenIdAsc()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var dimId = Guid.NewGuid();
        var verId = Guid.NewGuid();

        _repo.DimensionHeaders[dimId] = new ConceptHeaderDto(dimId, "mood", "Estado de ánimo");
        _repo.DimensionVersions[dimId] = new List<ConceptVersionHeaderDto> { new(verId, 1) };

        var sameTime = DateTimeOffset.UtcNow;
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        // Agregar en orden inverso
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(id2, verId, 1, 4, sameTime)));
        _repo.DimensionMeasurements.Add((userId, dimId, new DimensionMeasurementPointDto(id1, verId, 1, 2, sameTime)));

        var result = await _useCase.ExecuteAsync(new GetDimensionTrendsQuery(dimId));

        var series = Assert.Single(result.Series);
        Assert.Equal(id1, series.Points[0].CheckInId);
        Assert.Equal(id2, series.Points[1].CheckInId);
    }
}
