using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Contracts.Identity;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.RequestPasswordReset;
using Yusay.Application.Identity.Commands.ResetPassword;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Commands.SignOut;
using Yusay.Application.Identity.Commands.VerifyEmail;

namespace Yusay.Api.Controllers.Identity;

[ApiController]
[Route("auth")]
[Tags("Yusay.Api")]
public sealed class AuthController(
    IRegisterUserUseCase registerUserUseCase,
    IVerifyEmailUseCase verifyEmailUseCase,
    ISignInUseCase signInUseCase,
    ISignOutUseCase signOutUseCase,
    IRequestPasswordResetUseCase requestPasswordResetUseCase,
    IResetPasswordUseCase resetPasswordUseCase) : ControllerBase
{
    private readonly IRegisterUserUseCase _registerUserUseCase = registerUserUseCase;
    private readonly IVerifyEmailUseCase _verifyEmailUseCase = verifyEmailUseCase;
    private readonly ISignInUseCase _signInUseCase = signInUseCase;
    private readonly ISignOutUseCase _signOutUseCase = signOutUseCase;
    private readonly IRequestPasswordResetUseCase _requestPasswordResetUseCase = requestPasswordResetUseCase;
    private readonly IResetPasswordUseCase _resetPasswordUseCase = resetPasswordUseCase;

    [HttpPost("register")]
    [EndpointName("RegisterUser")]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<RegisterUserResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _registerUserUseCase.ExecuteAsync(
            new RegisterUserCommand(
                request.Email,
                request.Password,
                request.AdultConfirmed,
                request.AdultConfirmedAt),
            cancellationToken);

        return Created($"/auth/users/{result.UserId}", new RegisterUserResponse(result.UserId, result.Email));
    }

    [HttpPost("verify-email")]
    [EndpointName("VerifyEmail")]
    [ProducesResponseType(typeof(VerifyEmailResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<VerifyEmailResponse>> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _verifyEmailUseCase.ExecuteAsync(
            new VerifyEmailCommand(request.Token),
            cancellationToken);

        return Ok(new VerifyEmailResponse(result.UserId, result.Email, result.VerifiedAt));
    }

    [HttpPost("sign-in")]
    [EndpointName("SignIn")]
    [ProducesResponseType(typeof(SignInResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    public async Task<ActionResult<SignInResponse>> SignIn(
        SignInRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _signInUseCase.ExecuteAsync(
            new SignInCommand(request.Email, request.Password),
            cancellationToken);

        return Ok(new SignInResponse(
            result.UserId,
            result.Email,
            result.AccessToken,
            result.TokenType,
            result.IssuedAt,
            result.ExpiresAt));
    }

    [HttpPost("sign-out")]
    [EndpointName("SignOut")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> SignOut(CancellationToken cancellationToken)
    {
        const string bearerPrefix = "Bearer ";

        var authorization = Request.Headers.Authorization.ToString();
        if (!authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedException("Se requiere un token de acceso Bearer para cerrar la sesión.");
        }

        var accessToken = authorization[bearerPrefix.Length..].Trim();

        await _signOutUseCase.ExecuteAsync(new SignOutCommand(accessToken), cancellationToken);
        return NoContent();
    }

    [HttpPost("password-reset/request")]
    [EndpointName("RequestPasswordReset")]
    [ProducesResponseType(typeof(RequestPasswordResetResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<RequestPasswordResetResponse>> RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _requestPasswordResetUseCase.ExecuteAsync(
            new RequestPasswordResetCommand(request.Email),
            cancellationToken);

        return Ok(new RequestPasswordResetResponse(result.EmailSent));
    }

    [HttpPost("password-reset/confirm")]
    [EndpointName("ResetPassword")]
    [ProducesResponseType(typeof(ResetPasswordResponse), StatusCodes.Status200OK, "application/json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ResetPasswordResponse>> ResetPassword(
        ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _resetPasswordUseCase.ExecuteAsync(
            new ResetPasswordCommand(request.Token, request.NewPassword),
            cancellationToken);

        return Ok(new ResetPasswordResponse(result.UserId, result.Email));
    }
}
