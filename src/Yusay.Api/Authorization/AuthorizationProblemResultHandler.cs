using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;

namespace Yusay.Api.Authorization;

/// <summary>
/// Contrato 403 de la API (OQ-ARCH-017): centraliza la denegación de autorización para
/// identidades autenticadas como ProblemDetails con traceId (application/problem+json),
/// conforme al invariante 5 de authentication-invariants.md. Los desafíos sin autenticación
/// válida se delegan en ChallengeAsync del esquema, conservando intactos los contratos
/// actuales de 401 y 503; no se audita AUTHORIZATION_DENIED en este bloque (decisión
/// independiente pendiente).
/// </summary>
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
            // Camino por defecto del esquema Bearer: mismo desafío 401 que antes de existir
            // este manejador; el handler de autenticación manda su propio contrato.
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
