using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace Yusay.Api.Authorization;

public sealed class AuthorizationProblemResultHandler(
    IProblemDetailsService problemDetailsService) : IAuthorizationMiddlewareResultHandler
{
    private const string ForbiddenTitle = "Acceso denegado";
    private const string ForbiddenDetail =
        "La identidad autenticada no dispone de habilitación administrativa para este recurso.";

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            await context.ChallengeAsync();
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        await problemDetailsService.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = ForbiddenTitle,
                Detail = ForbiddenDetail,
                Instance = context.Request.Path
            }
        });
    }
}
