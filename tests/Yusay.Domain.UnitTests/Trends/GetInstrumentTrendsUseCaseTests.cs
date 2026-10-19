using Yusay.Application.Common.Exceptions;
using Yusay.Application.Trends.Queries.GetInstrumentTrends;
using Yusay.Application.Trends.Repositories;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Trends.Fakes;

namespace Yusay.Domain.UnitTests.Trends;

public sealed class GetInstrumentTrendsUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeTrendsRepository _repo = new();
    private readonly GetInstrumentTrendsUseCase _useCase;

    public GetInstrumentTrendsUseCaseTests()
    {
        _useCase = new GetInstrumentTrendsUseCase(_currentUser, _repo);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorizedException()
    {
        _currentUser.UserId = null;
        var query = new GetInstrumentTrendsQuery(Guid.NewGuid());

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _useCase.ExecuteAsync(query));
    }

    [Fact]
    public async Task ExecuteAsync_WithFromLaterThanTo_ShouldThrowValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var query = new GetInstrumentTrendsQuery(
            Guid.NewGuid(),
            From: now.AddDays(1),
            To: now);

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _useCase.ExecuteAsync(query));

        Assert.Equal("El parámetro 'from' no puede ser posterior al parámetro 'to'.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentInstrument_ShouldThrowNotFoundException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var query = new GetInstrumentTrendsQuery(Guid.NewGuid());

        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            _useCase.ExecuteAsync(query));

        Assert.Equal("El Instrumento solicitado no existe.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNoAssessmentsForUser_ShouldReturnEmptySeries()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var instId = Guid.NewGuid();

        _repo.InstrumentHeaders[instId] = new ConceptHeaderDto(instId, "PHQ-9", "Patient Health Questionnaire");
        _repo.InstrumentVersions[instId] = new List<ConceptVersionHeaderDto> { new(Guid.NewGuid(), 1) };

        var result = await _useCase.ExecuteAsync(new GetInstrumentTrendsQuery(instId));

        Assert.Equal(instId, result.InstrumentId);
        Assert.Equal("PHQ-9", result.InstrumentCode);
        Assert.Equal("Patient Health Questionnaire", result.InstrumentName);
        Assert.Empty(result.Series);
    }

    [Fact]
    public async Task ExecuteAsync_WithSingleVersion_ShouldCalculateContinuousStatisticsCorrectly()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var instId = Guid.NewGuid();
        var verId = Guid.NewGuid();

        _repo.InstrumentHeaders[instId] = new ConceptHeaderDto(instId, "GAD-7", "Generalized Anxiety Disorder 7");
        _repo.InstrumentVersions[instId] = new List<ConceptVersionHeaderDto> { new(verId, 1) };

        var baseTime = DateTimeOffset.UtcNow.AddDays(-10);
        var interpId = Guid.NewGuid();

        // 3 assessments: scores 5, 10, 11
        // average: (5 + 10 + 11) / 3 = 26 / 3 = 8.6666... -> 8.67
        // delta: 11 - 5 = 6
        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(
            Guid.NewGuid(), verId, 1, 5, baseTime.AddHours(1), interpId, "Leve")));
        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(
            Guid.NewGuid(), verId, 1, 10, baseTime.AddHours(2), interpId, "Moderado")));
        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(
            Guid.NewGuid(), verId, 1, 11, baseTime.AddHours(3), interpId, "Moderado")));

        var result = await _useCase.ExecuteAsync(new GetInstrumentTrendsQuery(instId));

        var series = Assert.Single(result.Series);
        Assert.Equal(verId, series.ReferenceVersionId);
        Assert.Equal(1, series.ReferenceVersionNumber);
        Assert.Equal(3, series.Points.Count);

        Assert.Equal(3, series.Summary.Count);
        Assert.Equal(5, series.Summary.Min);
        Assert.Equal(11, series.Summary.Max);
        Assert.Equal(5, series.Summary.FirstScore);
        Assert.Equal(11, series.Summary.LastScore);
        Assert.Equal(6, series.Summary.Delta);
        Assert.Equal(8.67m, series.Summary.Average);
    }

    [Fact]
    public async Task ExecuteAsync_WithV1V2AndV2V3Compatible_WithoutV1V3_ShouldGenerateTwoOverlappingSeries_WithoutTransitivity()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var instId = Guid.NewGuid();

        var v1Id = Guid.NewGuid();
        var v2Id = Guid.NewGuid();
        var v3Id = Guid.NewGuid();

        _repo.InstrumentHeaders[instId] = new ConceptHeaderDto(instId, "PHQ-9", "Patient Health Questionnaire");
        _repo.InstrumentVersions[instId] = new List<ConceptVersionHeaderDto>
        {
            new(v1Id, 1),
            new(v2Id, 2),
            new(v3Id, 3)
        };

        // Declaraciones explícitas: (V1, V2) y (V2, V3). No existe (V1, V3).
        _repo.InstrumentCompatibilityPairs[instId] = new List<CompatibilityPairDto>
        {
            new(v1Id, v2Id),
            new(v2Id, v3Id)
        };

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        var a1Id = Guid.NewGuid();
        var a2Id = Guid.NewGuid();
        var a3Id = Guid.NewGuid();
        var interpId = Guid.NewGuid();

        // A1 en V1 (score 6), A2 en V2 (score 8), A3 en V3 (score 4)
        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(
            a1Id, v1Id, 1, 6, t1, interpId, "Leve")));
        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(
            a2Id, v2Id, 2, 8, t2, interpId, "Leve")));
        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(
            a3Id, v3Id, 3, 4, t3, interpId, "Mínimo")));

        var result = await _useCase.ExecuteAsync(new GetInstrumentTrendsQuery(instId));

        Assert.Equal(2, result.Series.Count);

        // Serie 1: {V1, V2}, Referencia V2
        var s1 = result.Series[0];
        Assert.Equal(v2Id, s1.ReferenceVersionId);
        Assert.Equal(2, s1.ReferenceVersionNumber);
        Assert.Equal(2, s1.Points.Count);
        Assert.Contains(s1.Points, p => p.AttemptId == a1Id && p.VersionNumber == 1);
        Assert.Contains(s1.Points, p => p.AttemptId == a2Id && p.VersionNumber == 2);
        Assert.DoesNotContain(s1.Points, p => p.AttemptId == a3Id);
        Assert.Equal(2, s1.Summary.Delta); // 8 - 6 = 2
        Assert.Equal(7.00m, s1.Summary.Average);

        // Serie 2: {V2, V3}, Referencia V3
        var s2 = result.Series[1];
        Assert.Equal(v3Id, s2.ReferenceVersionId);
        Assert.Equal(3, s2.ReferenceVersionNumber);
        Assert.Equal(2, s2.Points.Count);
        Assert.Contains(s2.Points, p => p.AttemptId == a2Id && p.VersionNumber == 2);
        Assert.Contains(s2.Points, p => p.AttemptId == a3Id && p.VersionNumber == 3);
        Assert.DoesNotContain(s2.Points, p => p.AttemptId == a1Id);
        Assert.Equal(-4, s2.Summary.Delta); // 4 - 8 = -4
        Assert.Equal(6.00m, s2.Summary.Average);

        // A2 aparece en ambas series
        // V1 y V3 NUNCA aparecen juntas (No transitividad)
        Assert.DoesNotContain(result.Series, s =>
            s.Points.Any(p => p.VersionNumber == 1) && s.Points.Any(p => p.VersionNumber == 3));
    }

    [Fact]
    public async Task ExecuteAsync_WithTieBreak_ShouldSortByCalculatedAtAscThenAttemptIdAsc()
    {
        var userId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var instId = Guid.NewGuid();
        var verId = Guid.NewGuid();

        _repo.InstrumentHeaders[instId] = new ConceptHeaderDto(instId, "PHQ-9", "Patient Health Questionnaire");
        _repo.InstrumentVersions[instId] = new List<ConceptVersionHeaderDto> { new(verId, 1) };

        var sameTime = DateTimeOffset.UtcNow;
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var interpId = Guid.NewGuid();

        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(id2, verId, 1, 10, sameTime, interpId, "A")));
        _repo.InstrumentAssessments.Add((userId, instId, new InstrumentAssessmentPointDto(id1, verId, 1, 5, sameTime, interpId, "B")));

        var result = await _useCase.ExecuteAsync(new GetInstrumentTrendsQuery(instId));

        var series = Assert.Single(result.Series);
        Assert.Equal(id1, series.Points[0].AttemptId);
        Assert.Equal(id2, series.Points[1].AttemptId);
    }
}
