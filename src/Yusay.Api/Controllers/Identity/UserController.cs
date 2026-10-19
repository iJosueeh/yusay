using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Common;
using Yusay.Api.Contracts.Identity;
using Yusay.Application.Identity.Queries.GetUserProfile;

namespace Yusay.Api.Controllers.Identity;

[ApiController]
[Route("users")]
[Authorize]
[Tags("Yusay.Api")]
public sealed class UserController(IGetUserProfileUseCase getUserProfileUseCase) : ControllerBase
{
    private readonly IGetUserProfileUseCase _getUserProfileUseCase = getUserProfileUseCase;

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
}
