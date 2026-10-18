using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Assessment;
using Yusay.Application.Assessment.Queries.GetPublishedInstrumentById;
using Yusay.Application.Assessment.Queries.ListPublishedInstruments;

namespace Yusay.Api.Controllers.Assessment;

[ApiController]
[Route("instruments")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class InstrumentController(
    IListPublishedInstrumentsUseCase listPublishedInstrumentsUseCase,
    IGetPublishedInstrumentByIdUseCase getPublishedInstrumentByIdUseCase) : ControllerBase
{
    private readonly IListPublishedInstrumentsUseCase _listPublishedInstrumentsUseCase = listPublishedInstrumentsUseCase;
    private readonly IGetPublishedInstrumentByIdUseCase _getPublishedInstrumentByIdUseCase = getPublishedInstrumentByIdUseCase;

    [HttpGet]
    [EndpointName("ListPublishedInstruments")]
    [ProducesResponseType(typeof(ListPublishedInstrumentsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListPublishedInstrumentsResponse>> List(CancellationToken cancellationToken)
    {
        var items = await _listPublishedInstrumentsUseCase.ExecuteAsync(cancellationToken);
        return Ok(PublishedInstrumentContractMapper.ToListResponse(items));
    }

    [HttpGet("{instrumentId:guid}")]
    [EndpointName("GetPublishedInstrumentById")]
    [ProducesResponseType(typeof(PublishedInstrumentDetailsResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<PublishedInstrumentDetailsResponse>> GetById(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        var result = await _getPublishedInstrumentByIdUseCase.ExecuteAsync(instrumentId, cancellationToken);
        return Ok(PublishedInstrumentContractMapper.ToDetailsResponse(result));
    }
}
