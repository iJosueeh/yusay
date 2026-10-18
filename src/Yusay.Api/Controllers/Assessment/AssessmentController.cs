using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Assessment;
using Yusay.Application.Assessment.Commands.SaveAssessmentAnswers;
using Yusay.Application.Assessment.Commands.SubmitAssessmentAttempt;
using Yusay.Application.Assessment.Queries.GetAssessmentAttempt;

namespace Yusay.Api.Controllers.Assessment;

[ApiController]
[Route("assessments")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class AssessmentController(
    IGetAssessmentAttemptUseCase getAssessmentAttemptUseCase,
    ISaveAssessmentAnswersUseCase saveAssessmentAnswersUseCase,
    ISubmitAssessmentAttemptUseCase submitAssessmentAttemptUseCase) : ControllerBase
{
    private readonly IGetAssessmentAttemptUseCase _getAssessmentAttemptUseCase = getAssessmentAttemptUseCase;
    private readonly ISaveAssessmentAnswersUseCase _saveAssessmentAnswersUseCase = saveAssessmentAnswersUseCase;
    private readonly ISubmitAssessmentAttemptUseCase _submitAssessmentAttemptUseCase = submitAssessmentAttemptUseCase;

    [HttpGet("{attemptId:guid}")]
    [EndpointName("GetAssessmentAttempt")]
    [ProducesResponseType(typeof(AssessmentAttemptProgressResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<AssessmentAttemptProgressResponse>> GetAttempt(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var result = await _getAssessmentAttemptUseCase.ExecuteAsync(
            new GetAssessmentAttemptQuery(attemptId),
            cancellationToken);

        return Ok(AssessmentAttemptContractMapper.ToResponse(result));
    }

    [HttpPut("{attemptId:guid}/answers")]
    [EndpointName("SaveAssessmentAnswers")]
    [ProducesResponseType(typeof(SaveAssessmentAnswersResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<SaveAssessmentAnswersResponse>> SaveAnswers(
        Guid attemptId,
        SaveAssessmentAnswersRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _saveAssessmentAnswersUseCase.ExecuteAsync(
            new SaveAssessmentAnswersCommand(attemptId, AssessmentAttemptContractMapper.ToDtos(request.Answers)),
            cancellationToken);

        return Ok(AssessmentAttemptContractMapper.ToResponse(result));
    }

    [HttpPost("{attemptId:guid}/submit")]
    [EndpointName("SubmitAssessmentAttempt")]
    [ProducesResponseType(typeof(SubmitAssessmentAttemptResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<SubmitAssessmentAttemptResponse>> SubmitAssessment(
        Guid attemptId,
        CancellationToken cancellationToken)
    {
        var result = await _submitAssessmentAttemptUseCase.ExecuteAsync(
            new SubmitAssessmentAttemptCommand(attemptId),
            cancellationToken);

        return Ok(AssessmentAttemptContractMapper.ToResponse(result));
    }
}
