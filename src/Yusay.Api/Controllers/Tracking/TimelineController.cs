using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Contracts.Tracking;
using Yusay.Application.Tracking.Queries.GetTimeline;

namespace Yusay.Api.Controllers.Tracking;

[ApiController]
[Route("timeline")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class TimelineController(IGetTimelineUseCase getTimelineUseCase) : ControllerBase
{
    private readonly IGetTimelineUseCase _getTimelineUseCase = getTimelineUseCase;

    [HttpGet]
    [EndpointName("GetTimeline")]
    [ProducesResponseType(typeof(TimelineResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<TimelineResponse>> GetTimeline(
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? type,
        CancellationToken cancellationToken)
    {
        var result = await _getTimelineUseCase.ExecuteAsync(
            new GetTimelineQuery(limit, cursor, from, to, type),
            cancellationToken);

        var items = result.Items.Select(item => new TimelineItemResponse(
            item.EventId,
            item.Type,
            item.OccurredAt,
            item.Assessment is null ? null : new TimelineAssessmentResponse(
                item.Assessment.AttemptId,
                item.Assessment.InstrumentId,
                item.Assessment.InstrumentCode,
                item.Assessment.InstrumentName,
                item.Assessment.InstrumentVersionId,
                item.Assessment.Version,
                item.Assessment.Score,
                new TimelineInterpretationResponse(
                    item.Assessment.Interpretation.InterpretationId,
                    item.Assessment.Interpretation.Label)),
            item.CheckIn is null ? null : new TimelineCheckInResponse(
                item.CheckIn.CheckInId,
                item.CheckIn.Note,
                item.CheckIn.Measurements.Select(m => new TimelineMeasurementResponse(
                    m.DimensionId,
                    m.DimensionCode,
                    m.DimensionName,
                    m.DimensionVersionId,
                    m.Value)).ToList()))).ToList();

        return Ok(new TimelineResponse(items, result.NextCursor));
    }
}
