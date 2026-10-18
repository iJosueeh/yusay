using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Authorization;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Administration;
using Yusay.Application.Assessment.Commands.CreateInstrument;
using Yusay.Application.Assessment.Commands.CreateInstrumentVersion;
using Yusay.Application.Assessment.Commands.ReplaceDraftDefinition;
using Yusay.Application.Assessment.Commands.UpdateInstrument;
using Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentById;
using Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentVersionById;
using Yusay.Application.Assessment.Queries.ListAdministrativeInstruments;

namespace Yusay.Api.Controllers.Administration;

[ApiController]
[Route("admin/instruments")]
[Authorize(Policy = AdministratorPolicy.Name)]
[Tags("Yusay.Api")]
public sealed class InstrumentAdminController(
    ICreateInstrumentUseCase createInstrumentUseCase,
    IUpdateInstrumentUseCase updateInstrumentUseCase,
    ICreateInstrumentVersionUseCase createInstrumentVersionUseCase,
    IReplaceDraftDefinitionUseCase replaceDraftDefinitionUseCase,
    IListAdministrativeInstrumentsUseCase listAdministrativeInstrumentsUseCase,
    IGetAdministrativeInstrumentByIdUseCase getAdministrativeInstrumentByIdUseCase,
    IGetAdministrativeInstrumentVersionByIdUseCase getAdministrativeInstrumentVersionByIdUseCase) : ControllerBase
{
    private readonly ICreateInstrumentUseCase _createInstrumentUseCase = createInstrumentUseCase;
    private readonly IUpdateInstrumentUseCase _updateInstrumentUseCase = updateInstrumentUseCase;
    private readonly ICreateInstrumentVersionUseCase _createInstrumentVersionUseCase = createInstrumentVersionUseCase;
    private readonly IReplaceDraftDefinitionUseCase _replaceDraftDefinitionUseCase = replaceDraftDefinitionUseCase;
    private readonly IListAdministrativeInstrumentsUseCase _listAdministrativeInstrumentsUseCase =
        listAdministrativeInstrumentsUseCase;
    private readonly IGetAdministrativeInstrumentByIdUseCase _getAdministrativeInstrumentByIdUseCase =
        getAdministrativeInstrumentByIdUseCase;
    private readonly IGetAdministrativeInstrumentVersionByIdUseCase _getAdministrativeInstrumentVersionByIdUseCase =
        getAdministrativeInstrumentVersionByIdUseCase;

    [HttpGet]
    [EndpointName("ListAdministrativeInstruments")]
    [ProducesResponseType(typeof(ListInstrumentsAdminResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<ListInstrumentsAdminResponse>> List(CancellationToken cancellationToken)
    {
        var items = await _listAdministrativeInstrumentsUseCase.ExecuteAsync(cancellationToken);
        return Ok(InstrumentContractMapper.ToListResponse(items));
    }

    [HttpGet("{instrumentId:guid}")]
    [EndpointName("GetAdministrativeInstrumentById")]
    [ProducesResponseType(typeof(InstrumentDetailsAdminResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<InstrumentDetailsAdminResponse>> GetById(
        Guid instrumentId,
        CancellationToken cancellationToken)
    {
        var details = await _getAdministrativeInstrumentByIdUseCase.ExecuteAsync(instrumentId, cancellationToken);
        return Ok(InstrumentContractMapper.ToDetailsResponse(details));
    }

    [HttpPost]
    [EndpointName("CreateInstrument")]
    [ProducesResponseType(typeof(InstrumentResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<InstrumentResponse>> Create(
        CreateInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createInstrumentUseCase.ExecuteAsync(
            new CreateInstrumentCommand(request.Code, request.Name, request.Description, request.Purpose),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, InstrumentContractMapper.ToResponse(result));
    }

    [HttpPatch("{instrumentId:guid}")]
    [EndpointName("UpdateInstrument")]
    [ProducesResponseType(typeof(InstrumentResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<InstrumentResponse>> Update(
        Guid instrumentId,
        UpdateInstrumentRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _updateInstrumentUseCase.ExecuteAsync(
            new UpdateInstrumentCommand(instrumentId, request.Name, request.Description, request.Purpose),
            cancellationToken);

        return Ok(InstrumentContractMapper.ToResponse(result));
    }

    [HttpPost("{instrumentId:guid}/versions")]
    [EndpointName("CreateInstrumentVersion")]
    [ProducesResponseType(typeof(InstrumentVersionResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<InstrumentVersionResponse>> CreateVersion(
        Guid instrumentId,
        CreateInstrumentVersionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _createInstrumentVersionUseCase.ExecuteAsync(
            new CreateInstrumentVersionCommand(
                instrumentId,
                request.Version,
                request.SourceDescription,
                request.Population,
                request.AdministrationConditions,
                request.LicenseInformation,
                request.Limitations),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, InstrumentContractMapper.ToResponse(result));
    }

    [HttpGet("{instrumentId:guid}/versions/{versionId:guid}")]
    [EndpointName("GetAdministrativeInstrumentVersionById")]
    [ProducesResponseType(typeof(InstrumentVersionResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<InstrumentVersionResponse>> GetVersionById(
        Guid instrumentId,
        Guid versionId,
        CancellationToken cancellationToken)
    {
        var result = await _getAdministrativeInstrumentVersionByIdUseCase.ExecuteAsync(
            instrumentId, versionId, cancellationToken);

        return Ok(InstrumentContractMapper.ToResponse(result));
    }

    [HttpPut("{instrumentId:guid}/versions/{versionId:guid}/draft-definition")]
    [EndpointName("ReplaceDraftDefinition")]
    [ProducesResponseType(typeof(DraftDefinitionResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DraftDefinitionResponse>> ReplaceDraftDefinition(
        Guid instrumentId,
        Guid versionId,
        ReplaceDraftDefinitionRequest request,
        CancellationToken cancellationToken)
    {
        var references = (request.References ?? [])
            .Select(r => new DraftReferenceInput(r.ReferenceOrder, r.Citation, r.Url))
            .ToList();

        var questions = (request.Questions ?? [])
            .Select(q => new DraftQuestionInput(
                q.Position,
                q.Prompt,
                q.Required,
                (q.Options ?? []).Select(o => new DraftOptionInput(o.Position, o.Label, o.Contribution)).ToList()))
            .ToList();

        var interpretations = (request.Interpretations ?? [])
            .Select(i => new DraftInterpretationInput(
                i.Label,
                i.Description,
                i.Limitations,
                i.LowerBound,
                i.UpperBound))
            .ToList();

        var result = await _replaceDraftDefinitionUseCase.ExecuteAsync(
            new ReplaceDraftDefinitionCommand(
                instrumentId,
                versionId,
                references,
                questions,
                interpretations),
            cancellationToken);

        return Ok(InstrumentContractMapper.ToResponse(result));
    }
}
