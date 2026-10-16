using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Authorization;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Administration;
using Yusay.Api.Contracts.Tracking;
using Yusay.Application.Tracking.Commands.CreateDimension;
using Yusay.Application.Tracking.Commands.UpdateDimension;
using Yusay.Application.Tracking.Queries.ListAdministrativeDimensions;

namespace Yusay.Api.Controllers.Administration;

// B4 y B5a de RF-021: lectura completa del catálogo (GET) y escritura administrativa
// (POST/PATCH) bajo la política Administrator (OQ-ARCH-017). Las escrituras responden con
// la representación fina DimensionDetailsResponse, sin colección de versiones (B5b).
[ApiController]
[Route("admin/dimensions")]
[Authorize(Policy = AdministratorPolicy.Name)]
[Tags("Yusay.Api")]
public sealed class DimensionAdminController(
    IListAdministrativeDimensionsUseCase listAdministrativeDimensionsUseCase,
    ICreateDimensionUseCase createDimensionUseCase,
    IUpdateDimensionUseCase updateDimensionUseCase) : ControllerBase
{
    private readonly IListAdministrativeDimensionsUseCase _listAdministrativeDimensionsUseCase =
        listAdministrativeDimensionsUseCase;
    private readonly ICreateDimensionUseCase _createDimensionUseCase = createDimensionUseCase;
    private readonly IUpdateDimensionUseCase _updateDimensionUseCase = updateDimensionUseCase;

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

        // Sin header Location: no existe un GET de detalle administrativo al que apuntar, y
        // GET /dimensions/{id} devuelve 404 para una Dimensión aún sin versiones (B4).
        // La representación creada viaja completa en el cuerpo del 201.
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
}
