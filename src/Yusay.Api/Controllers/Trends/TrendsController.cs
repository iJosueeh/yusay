using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Contracts.Trends;
using Yusay.Application.Trends.Queries.GetDimensionTrends;
using Yusay.Application.Trends.Queries.GetInstrumentTrends;

namespace Yusay.Api.Controllers.Trends;

[ApiController]
[Route("trends")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class TrendsController(
    IGetDimensionTrendsUseCase getDimensionTrendsUseCase,
    IGetInstrumentTrendsUseCase getInstrumentTrendsUseCase) : ControllerBase
{
    private readonly IGetDimensionTrendsUseCase _getDimensionTrendsUseCase = getDimensionTrendsUseCase;
    private readonly IGetInstrumentTrendsUseCase _getInstrumentTrendsUseCase = getInstrumentTrendsUseCase;

    [HttpGet("dimensions/{dimensionId:guid}")]
    [EndpointName("GetDimensionTrends")]
    [ProducesResponseType(typeof(DimensionTrendResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionTrendResponse>> GetDimensionTrends(
        Guid dimensionId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var result = await _getDimensionTrendsUseCase.ExecuteAsync(
            new GetDimensionTrendsQuery(dimensionId, from, to),
            cancellationToken);

        var series = result.Series.Select(s => new DimensionTrendSeriesResponse(
            s.ReferenceVersionId,
            s.ReferenceVersionNumber,
            s.Versions.Select(v => new TrendVersionResponse(v.VersionId, v.VersionNumber)).ToList(),
            s.Points.Select(p => new DimensionTrendPointResponse(
                p.CheckInId,
                p.DimensionVersionId,
                p.VersionNumber,
                p.Value,
                p.RecordedAt)).ToList(),
            new DimensionTrendSummaryResponse(
                s.Summary.Count,
                s.Summary.Min,
                s.Summary.Max,
                s.Summary.Median,
                s.Summary.FirstValue,
                s.Summary.LastValue,
                s.Summary.Frequencies))).ToList();

        return Ok(new DimensionTrendResponse(
            result.DimensionId,
            result.DimensionCode,
            result.DimensionName,
            series));
    }

    [HttpGet("instruments/{instrumentId:guid}")]
    [EndpointName("GetInstrumentTrends")]
    [ProducesResponseType(typeof(InstrumentTrendResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<InstrumentTrendResponse>> GetInstrumentTrends(
        Guid instrumentId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken)
    {
        var result = await _getInstrumentTrendsUseCase.ExecuteAsync(
            new GetInstrumentTrendsQuery(instrumentId, from, to),
            cancellationToken);

        var series = result.Series.Select(s => new InstrumentTrendSeriesResponse(
            s.ReferenceVersionId,
            s.ReferenceVersionNumber,
            s.Versions.Select(v => new TrendVersionResponse(v.VersionId, v.VersionNumber)).ToList(),
            s.Points.Select(p => new InstrumentTrendPointResponse(
                p.AttemptId,
                p.InstrumentVersionId,
                p.VersionNumber,
                p.Score,
                p.CalculatedAt,
                new TrendInterpretationResponse(
                    p.Interpretation.InterpretationId,
                    p.Interpretation.Label))).ToList(),
            new InstrumentTrendSummaryResponse(
                s.Summary.Count,
                s.Summary.Min,
                s.Summary.Max,
                s.Summary.Average,
                s.Summary.Delta,
                s.Summary.FirstScore,
                s.Summary.LastScore))).ToList();

        return Ok(new InstrumentTrendResponse(
            result.InstrumentId,
            result.InstrumentCode,
            result.InstrumentName,
            series));
    }
}
