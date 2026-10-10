using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Contracts.Tracking;
using Yusay.Application.Tracking.Commands.CreateCheckIn;
using Yusay.Application.Tracking.Commands.DeleteCheckIn;
using Yusay.Application.Tracking.Commands.UpdateCheckIn;
using Yusay.Application.Tracking.Queries.GetCheckInById;
using Yusay.Application.Tracking.Queries.ListCheckIns;

namespace Yusay.Api.Controllers.Tracking;

[ApiController]
[Route("check-ins")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class CheckInController(
    ICreateCheckInUseCase createCheckInUseCase,
    IGetCheckInByIdUseCase getCheckInByIdUseCase,
    IUpdateCheckInUseCase updateCheckInUseCase,
    IDeleteCheckInUseCase deleteCheckInUseCase,
    IListCheckInsUseCase listCheckInsUseCase) : ControllerBase
{
    private readonly ICreateCheckInUseCase _createCheckInUseCase = createCheckInUseCase;
    private readonly IGetCheckInByIdUseCase _getCheckInByIdUseCase = getCheckInByIdUseCase;
    private readonly IUpdateCheckInUseCase _updateCheckInUseCase = updateCheckInUseCase;
    private readonly IDeleteCheckInUseCase _deleteCheckInUseCase = deleteCheckInUseCase;
    private readonly IListCheckInsUseCase _listCheckInsUseCase = listCheckInsUseCase;

    [HttpPost]
    [EndpointName("CreateCheckIn")]
    [ProducesResponseType(typeof(CreateCheckInResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<CreateCheckInResponse>> Create(
        CreateCheckInRequest request,
        CancellationToken cancellationToken)
    {
        var measurements = (request.Measurements ?? Array.Empty<CreateCheckInMeasurementRequest>())
            .Select(measurement => new CreateMeasurementInput(measurement.DimensionId, measurement.Value))
            .ToArray();

        var result = await _createCheckInUseCase.ExecuteAsync(
            new CreateCheckInCommand(request.RecordedAt, request.Note, measurements),
            cancellationToken);

        return Created(
            $"/check-ins/{result.CheckInId}",
            new CreateCheckInResponse(result.CheckInId, result.RecordedAt, result.CreatedAt, result.Revision));
    }

    [HttpGet]
    [EndpointName("ListCheckIns")]
    [ProducesResponseType(typeof(ListCheckInsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListCheckInsResponse>> List(
        int? limit,
        string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _listCheckInsUseCase.ExecuteAsync(
            new ListCheckInsQuery(limit, cursor),
            cancellationToken);

        return Ok(new ListCheckInsResponse(
            result.Items.Select(MapItem).ToArray(),
            result.NextCursor));
    }

    [HttpGet("{checkInId:guid}")]
    [EndpointName("GetCheckInById")]
    [ProducesResponseType(typeof(GetCheckInByIdResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<GetCheckInByIdResponse>> GetById(
        Guid checkInId,
        CancellationToken cancellationToken)
    {
        var result = await _getCheckInByIdUseCase.ExecuteAsync(
            new GetCheckInByIdQuery(checkInId),
            cancellationToken);

        return Ok(new GetCheckInByIdResponse(
            result.CheckInId,
            result.RecordedAt,
            result.CreatedAt,
            result.UpdatedAt,
            result.Revision,
            result.Note,
            result.Measurements
                .Select(measurement => new GetCheckInMeasurementResponse(
                    measurement.DimensionId,
                    measurement.DimensionVersionId,
                    measurement.Value))
                .ToArray()));
    }

    [HttpPut("{checkInId:guid}")]
    [EndpointName("UpdateCheckIn")]
    [ProducesResponseType(typeof(GetCheckInByIdResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<GetCheckInByIdResponse>> Update(
        Guid checkInId,
        UpdateCheckInRequest request,
        CancellationToken cancellationToken)
    {
        var measurements = (request.Measurements ?? Array.Empty<UpdateCheckInMeasurementRequest>())
            .Select(measurement => new UpdateMeasurementInput(measurement.DimensionId, measurement.Value))
            .ToArray();

        var result = await _updateCheckInUseCase.ExecuteAsync(
            new UpdateCheckInCommand(checkInId, request.Revision, request.RecordedAt, request.Note, measurements),
            cancellationToken);

        return Ok(new GetCheckInByIdResponse(
            result.CheckInId,
            result.RecordedAt,
            result.CreatedAt,
            result.UpdatedAt,
            result.Revision,
            result.Note,
            result.Measurements
                .Select(measurement => new GetCheckInMeasurementResponse(
                    measurement.DimensionId,
                    measurement.DimensionVersionId,
                    measurement.Value))
                .ToArray()));
    }

    [HttpDelete("{checkInId:guid}")]
    [EndpointName("DeleteCheckIn")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult> Delete(
        Guid checkInId,
        DeleteCheckInRequest request,
        CancellationToken cancellationToken)
    {
        await _deleteCheckInUseCase.ExecuteAsync(
            new DeleteCheckInCommand(checkInId, request.Revision),
            cancellationToken);

        return NoContent();
    }

    private static GetCheckInByIdResponse MapItem(GetCheckInByIdResult item) => new(
        item.CheckInId,
        item.RecordedAt,
        item.CreatedAt,
        item.UpdatedAt,
        item.Revision,
        item.Note,
        item.Measurements
            .Select(measurement => new GetCheckInMeasurementResponse(
                measurement.DimensionId,
                measurement.DimensionVersionId,
                measurement.Value))
            .ToArray());
}
