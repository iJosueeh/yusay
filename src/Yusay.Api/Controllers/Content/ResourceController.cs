using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Content;
using Yusay.Application.Content.Queries.GetPublishedResourceById;
using Yusay.Application.Content.Queries.ListPublishedResources;

namespace Yusay.Api.Controllers.Content;

[ApiController]
[Route("resources")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class ResourceController(
    IListPublishedResourcesUseCase listPublishedResourcesUseCase,
    IGetPublishedResourceByIdUseCase getPublishedResourceByIdUseCase) : ControllerBase
{
    private readonly IListPublishedResourcesUseCase _listPublishedResourcesUseCase = listPublishedResourcesUseCase;
    private readonly IGetPublishedResourceByIdUseCase _getPublishedResourceByIdUseCase = getPublishedResourceByIdUseCase;

    [HttpGet]
    [EndpointName("ListPublishedResources")]
    [ProducesResponseType(typeof(ListResourcesResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListResourcesResponse>> List(
        [FromQuery] Guid? topicId,
        [FromQuery] Guid? dimensionId,
        [FromQuery] Guid? attemptId,
        [FromQuery] string? type,
        [FromQuery] int? limit,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
    {
        var result = await _listPublishedResourcesUseCase.ExecuteAsync(
            new ListPublishedResourcesQuery(
                TopicId: topicId,
                DimensionId: dimensionId,
                AttemptId: attemptId,
                Type: type,
                Limit: limit,
                Cursor: cursor),
            cancellationToken);

        return Ok(PublishedContentContractMapper.ToListResponse(result));
    }

    [HttpGet("{resourceId:guid}")]
    [EndpointName("GetPublishedResourceById")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> GetById(
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var result = await _getPublishedResourceByIdUseCase.ExecuteAsync(resourceId, cancellationToken);
        return Ok(PublishedContentContractMapper.ToResponse(result));
    }
}
