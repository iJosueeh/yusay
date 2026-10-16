using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Tracking;
using Yusay.Application.Tracking.Queries.GetDimensionById;
using Yusay.Application.Tracking.Queries.ListDimensions;

namespace Yusay.Api.Controllers.Tracking;

[ApiController]
[Route("dimensions")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class DimensionController(
    IListDimensionsUseCase listDimensionsUseCase,
    IGetDimensionByIdUseCase getDimensionByIdUseCase) : ControllerBase
{
    private readonly IListDimensionsUseCase _listDimensionsUseCase = listDimensionsUseCase;
    private readonly IGetDimensionByIdUseCase _getDimensionByIdUseCase = getDimensionByIdUseCase;

    [HttpGet]
    [EndpointName("ListDimensions")]
    [ProducesResponseType(typeof(ListDimensionsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListDimensionsResponse>> List(CancellationToken cancellationToken)
    {
        var items = await _listDimensionsUseCase.ExecuteAsync(cancellationToken);

        return Ok(DimensionContractMapper.ToListResponse(items));
    }

    [HttpGet("{dimensionId:guid}")]
    [EndpointName("GetDimensionById")]
    [ProducesResponseType(typeof(DimensionResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DimensionResponse>> GetById(
        Guid dimensionId,
        CancellationToken cancellationToken)
    {
        var dimension = await _getDimensionByIdUseCase.ExecuteAsync(
            new GetDimensionByIdQuery(dimensionId), cancellationToken);

        return Ok(DimensionContractMapper.ToResponse(dimension));
    }
}
