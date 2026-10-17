using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Authorization;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Administration;
using Yusay.Api.Contracts.Tracking;
using Yusay.Application.Tracking.Commands;
using Yusay.Application.Tracking.Commands.ActivateDimensionVersion;
using Yusay.Application.Tracking.Commands.CreateDimension;
using Yusay.Application.Tracking.Commands.CreateDimensionVersion;
using Yusay.Application.Tracking.Commands.RetireDimensionVersion;
using Yusay.Application.Tracking.Commands.UpdateDimension;
using Yusay.Application.Tracking.Commands.UpdateDimensionVersion;
using Yusay.Application.Tracking.Queries.ListAdministrativeDimensions;

namespace Yusay.Api.Controllers.Administration;

[ApiController]
[Route("admin/dimensions")]
[Authorize(Policy = AdministratorPolicy.Name)]
[Tags("Yusay.Api")]
public sealed class DimensionAdminController(
    IListAdministrativeDimensionsUseCase listAdministrativeDimensionsUseCase,
    ICreateDimensionUseCase createDimensionUseCase,
    IUpdateDimensionUseCase updateDimensionUseCase,
    ICreateDimensionVersionUseCase createDimensionVersionUseCase,
    IUpdateDimensionVersionUseCase updateDimensionVersionUseCase,
    IActivateDimensionVersionUseCase activateDimensionVersionUseCase,
    IRetireDimensionVersionUseCase retireDimensionVersionUseCase) : ControllerBase
{
    private readonly IListAdministrativeDimensionsUseCase _listAdministrativeDimensionsUseCase =
        listAdministrativeDimensionsUseCase;
    private readonly ICreateDimensionUseCase _createDimensionUseCase = createDimensionUseCase;
    private readonly IUpdateDimensionUseCase _updateDimensionUseCase = updateDimensionUseCase;
    private readonly ICreateDimensionVersionUseCase _createDimensionVersionUseCase =
        createDimensionVersionUseCase;
    private readonly IUpdateDimensionVersionUseCase _updateDimensionVersionUseCase =
        updateDimensionVersionUseCase;
    private readonly IActivateDimensionVersionUseCase _activateDimensionVersionUseCase =
        activateDimensionVersionUseCase;
    private readonly IRetireDimensionVersionUseCase _retireDimensionVersionUseCase =
        retireDimensionVersionUseCase;

    [HttpGet]
    [EndpointName("ListAdministrativeDimensions")]
    [ProducesResponseType(typeof(ListDimensionsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListDimensionsResponse>> List(CancellationToken cancellationToken)
    {
        var items = await _listAdministrativeDimensionsUseCase.ExecuteAsync(cancellationToken);

        return Ok(DimensionContractMapper.ToListResponse(items));
    }

    [HttpPost]
    [EndpointName("CreateDimension")]
    [ProducesResponseType(typeof(DimensionDetailsResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionDetailsResponse>> Create(
        CreateDimensionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createDimensionUseCase.ExecuteAsync(
            new CreateDimensionCommand(request.Code, request.Name, request.Description),
            cancellationToken);

        return StatusCode(
            StatusCodes.Status201Created,
            new DimensionDetailsResponse(result.DimensionId, result.Code, result.Name, result.Description));
    }

    [HttpPatch("{dimensionId:guid}")]
    [EndpointName("UpdateDimension")]
    [ProducesResponseType(typeof(DimensionDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionDetailsResponse>> Update(
        Guid dimensionId,
        UpdateDimensionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateDimensionUseCase.ExecuteAsync(
            new UpdateDimensionCommand(dimensionId, request.Name, request.Description),
            cancellationToken);

        return Ok(new DimensionDetailsResponse(result.DimensionId, result.Code, result.Name, result.Description));
    }

    [HttpPost("{dimensionId:guid}/versions")]
    [EndpointName("CreateDimensionVersion")]
    [ProducesResponseType(typeof(DimensionVersionResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionVersionResponse>> CreateVersion(
        Guid dimensionId,
        CreateDimensionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createDimensionVersionUseCase.ExecuteAsync(
            new CreateDimensionVersionCommand(
                dimensionId,
                request.Version,
                request.Definition,
                request.MinValue,
                request.MaxValue,
                request.Step,
                request.Anchors?
                    .Select(anchor => new AnchorInput(anchor.Value, anchor.Label))
                    .ToArray()),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, DimensionContractMapper.ToResponse(result));
    }

    [HttpPatch("{dimensionId:guid}/versions/{dimensionVersionId:guid}")]
    [EndpointName("UpdateDimensionVersion")]
    [ProducesResponseType(typeof(DimensionVersionResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionVersionResponse>> UpdateVersion(
        Guid dimensionId,
        Guid dimensionVersionId,
        UpdateDimensionVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateDimensionVersionUseCase.ExecuteAsync(
            new UpdateDimensionVersionCommand(
                dimensionId,
                dimensionVersionId,
                request.Definition,
                request.MinValue,
                request.MaxValue,
                request.Step,
                request.Anchors?
                    .Select(anchor => new AnchorInput(anchor.Value, anchor.Label))
                    .ToArray()),
            cancellationToken);

        return Ok(DimensionContractMapper.ToResponse(result));
    }

    [HttpPost("{dimensionId:guid}/versions/{dimensionVersionId:guid}/activation")]
    [EndpointName("ActivateDimensionVersion")]
    [ProducesResponseType(typeof(DimensionVersionResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionVersionResponse>> ActivateVersion(
        Guid dimensionId,
        Guid dimensionVersionId,
        CancellationToken cancellationToken)
    {
        var result = await _activateDimensionVersionUseCase.ExecuteAsync(
            new ActivateDimensionVersionCommand(dimensionId, dimensionVersionId),
            cancellationToken);

        return Ok(DimensionContractMapper.ToResponse(result));
    }

    [HttpPost("{dimensionId:guid}/versions/{dimensionVersionId:guid}/retirement")]
    [EndpointName("RetireDimensionVersion")]
    [ProducesResponseType(typeof(DimensionVersionResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionVersionResponse>> RetireVersion(
        Guid dimensionId,
        Guid dimensionVersionId,
        CancellationToken cancellationToken)
    {
        var result = await _retireDimensionVersionUseCase.ExecuteAsync(
            new RetireDimensionVersionCommand(dimensionId, dimensionVersionId),
            cancellationToken);

        return Ok(DimensionContractMapper.ToResponse(result));
    }
}
