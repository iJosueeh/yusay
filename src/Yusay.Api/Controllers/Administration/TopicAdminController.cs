using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Authorization;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Administration;
using Yusay.Application.Content.Commands.CreateTopic;
using Yusay.Application.Content.Queries.ListTopics;

namespace Yusay.Api.Controllers.Administration;

[ApiController]
[Route("admin/topics")]
[Authorize(Policy = AdministratorPolicy.Name)]
[Tags("Yusay.Api")]
public sealed class TopicAdminController(
    IListTopicsUseCase listTopicsUseCase,
    ICreateTopicUseCase createTopicUseCase) : ControllerBase
{
    private readonly IListTopicsUseCase _listTopicsUseCase = listTopicsUseCase;
    private readonly ICreateTopicUseCase _createTopicUseCase = createTopicUseCase;

    [HttpGet]
    [EndpointName("ListAdministrativeTopics")]
    [ProducesResponseType(typeof(ListTopicsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListTopicsResponse>> List(CancellationToken cancellationToken)
    {
        var items = await _listTopicsUseCase.ExecuteAsync(cancellationToken);
        return Ok(ContentContractMapper.ToListResponse(items));
    }

    [HttpPost]
    [EndpointName("CreateTopic")]
    [ProducesResponseType(typeof(TopicResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<TopicResponse>> Create(
        CreateTopicRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createTopicUseCase.ExecuteAsync(
            new CreateTopicCommand(request.Code, request.Name, request.Description),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ContentContractMapper.ToResponse(result));
    }
}
