using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Identity;
using Yusay.Application.Identity.Commands.DeleteUserAccount;
using Yusay.Application.Identity.Queries.GetUserProfile;

namespace Yusay.Api.Controllers.Identity;

[ApiController]
[Route("users")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class UserController(
    IGetUserProfileUseCase getUserProfileUseCase,
    IDeleteUserAccountUseCase deleteUserAccountUseCase) : ControllerBase
{
    private readonly IGetUserProfileUseCase _getUserProfileUseCase = getUserProfileUseCase;
    private readonly IDeleteUserAccountUseCase _deleteUserAccountUseCase = deleteUserAccountUseCase;

    [HttpGet("me")]
    [EndpointName("GetCurrentUserProfile")]
    [ProducesResponseType(typeof(UserProfileResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<UserProfileResponse>> GetCurrentUserProfile(CancellationToken cancellationToken)
    {
        var result = await _getUserProfileUseCase.ExecuteAsync(new GetUserProfileQuery(), cancellationToken);
        return Ok(UserProfileContractMapper.ToResponse(result));
    }

    [HttpDelete("me")]
    [EndpointName("DeleteCurrentUserAccount")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> DeleteCurrentUserAccount(
        DeleteUserAccountRequest? request,
        CancellationToken cancellationToken)
    {
        string? accessToken = null;
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            accessToken = authHeader["Bearer ".Length..].Trim();
        }

        await _deleteUserAccountUseCase.ExecuteAsync(
            new DeleteUserAccountCommand(request?.Password ?? string.Empty, accessToken),
            cancellationToken);

        return NoContent();
    }
}
