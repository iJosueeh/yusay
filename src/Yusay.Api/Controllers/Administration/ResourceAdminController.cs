using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Authorization;
using Yusay.Api.Contracts.Administration;
using Yusay.Application.Content.Commands.AddResourceTopic;
using Yusay.Application.Content.Commands.CreateResource;
using Yusay.Application.Content.Commands.PublishResource;
using Yusay.Application.Content.Commands.RemoveResourceTopic;
using Yusay.Application.Content.Commands.RetireResource;
using Yusay.Application.Content.Commands.UpdateResource;
using Yusay.Application.Content.Queries.GetResourceById;
using Yusay.Application.Content.Queries.ListResources;

namespace Yusay.Api.Controllers.Administration;

[ApiController]
[Route("admin/resources")]
[Authorize(Policy = AdministratorPolicy.Name)]
[Tags("Yusay.Api")]
public sealed class ResourceAdminController(
    IListResourcesUseCase listResourcesUseCase,
    IGetResourceByIdUseCase getResourceByIdUseCase,
    ICreateResourceUseCase createResourceUseCase,
    IUpdateResourceUseCase updateResourceUseCase,
    IPublishResourceUseCase publishResourceUseCase,
    IRetireResourceUseCase retireResourceUseCase,
    IAddResourceTopicUseCase addResourceTopicUseCase,
    IRemoveResourceTopicUseCase removeResourceTopicUseCase) : ControllerBase
{
    private readonly IListResourcesUseCase _listResourcesUseCase = listResourcesUseCase;
    private readonly IGetResourceByIdUseCase _getResourceByIdUseCase = getResourceByIdUseCase;
    private readonly ICreateResourceUseCase _createResourceUseCase = createResourceUseCase;
    private readonly IUpdateResourceUseCase _updateResourceUseCase = updateResourceUseCase;
    private readonly IPublishResourceUseCase _publishResourceUseCase = publishResourceUseCase;
    private readonly IRetireResourceUseCase _retireResourceUseCase = retireResourceUseCase;
    private readonly IAddResourceTopicUseCase _addResourceTopicUseCase = addResourceTopicUseCase;
    private readonly IRemoveResourceTopicUseCase _removeResourceTopicUseCase = removeResourceTopicUseCase;

    [HttpGet]
    [EndpointName("ListAdministrativeResources")]
    [ProducesResponseType(typeof(ListResourcesResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListResourcesResponse>> List(
        [FromQuery] string? status,
        [FromQuery] string? type,
        [FromQuery] Guid? topicId,
        CancellationToken cancellationToken)
    {
        var items = await _listResourcesUseCase.ExecuteAsync(
            new ListResourcesQuery(status, type, topicId),
            cancellationToken);

        return Ok(ContentContractMapper.ToListResponse(items));
    }

    [HttpGet("{resourceId:guid}")]
    [EndpointName("GetAdministrativeResourceById")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> GetById(
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var result = await _getResourceByIdUseCase.ExecuteAsync(resourceId, cancellationToken);
        return Ok(ContentContractMapper.ToResponse(result));
    }

    [HttpPost]
    [EndpointName("CreateResource")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> Create(
        CreateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createResourceUseCase.ExecuteAsync(
            new CreateResourceCommand(
                request.Type,
                request.Title,
                request.Summary,
                request.Body,
                request.ExternalUrl,
                request.TopicIds),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ContentContractMapper.ToResponse(result));
    }

    [HttpPut("{resourceId:guid}")]
    [EndpointName("UpdateResource")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> Update(
        Guid resourceId,
        UpdateResourceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateResourceUseCase.ExecuteAsync(
            new UpdateResourceCommand(
                resourceId,
                request.Type,
                request.Title,
                request.Summary,
                request.Body,
                request.ExternalUrl,
                request.CorrectionKind),
            cancellationToken);

        return Ok(ContentContractMapper.ToResponse(result));
    }

    [HttpPost("{resourceId:guid}/publish")]
    [EndpointName("PublishResource")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> Publish(
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var result = await _publishResourceUseCase.ExecuteAsync(
            new PublishResourceCommand(resourceId),
            cancellationToken);

        return Ok(ContentContractMapper.ToResponse(result));
    }

    [HttpPost("{resourceId:guid}/retire")]
    [EndpointName("RetireResource")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> Retire(
        Guid resourceId,
        CancellationToken cancellationToken)
    {
        var result = await _retireResourceUseCase.ExecuteAsync(
            new RetireResourceCommand(resourceId),
            cancellationToken);

        return Ok(ContentContractMapper.ToResponse(result));
    }

    [HttpPost("{resourceId:guid}/topics/{topicId:guid}")]
    [EndpointName("AddResourceTopic")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> AddTopic(
        Guid resourceId,
        Guid topicId,
        CancellationToken cancellationToken)
    {
        var result = await _addResourceTopicUseCase.ExecuteAsync(
            new AddResourceTopicCommand(resourceId, topicId),
            cancellationToken);

        return Ok(ContentContractMapper.ToResponse(result));
    }

    [HttpDelete("{resourceId:guid}/topics/{topicId:guid}")]
    [EndpointName("RemoveResourceTopic")]
    [ProducesResponseType(typeof(ResourceDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ResourceDetailsResponse>> RemoveTopic(
        Guid resourceId,
        Guid topicId,
        CancellationToken cancellationToken)
    {
        var result = await _removeResourceTopicUseCase.ExecuteAsync(
            new RemoveResourceTopicCommand(resourceId, topicId),
            cancellationToken);

        return Ok(ContentContractMapper.ToResponse(result));
    }
}
