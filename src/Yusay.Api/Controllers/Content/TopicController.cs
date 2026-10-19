using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Content;
using Yusay.Application.Content.Queries.ListTopics;

namespace Yusay.Api.Controllers.Content;

[ApiController]
[Route("topics")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class TopicController(IListTopicsUseCase listTopicsUseCase) : ControllerBase
{
    private readonly IListTopicsUseCase _listTopicsUseCase = listTopicsUseCase;

    [HttpGet]
    [EndpointName("ListPublishedTopics")]
    [ProducesResponseType(typeof(ListTopicsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListTopicsResponse>> List(CancellationToken cancellationToken)
    {
        var items = await _listTopicsUseCase.ExecuteAsync(cancellationToken);
        return Ok(PublishedContentContractMapper.ToListResponse(items));
    }
}
