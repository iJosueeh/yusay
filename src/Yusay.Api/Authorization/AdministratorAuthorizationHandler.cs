using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Api.Authorization;

/// <summary>
/// Evaluación del requisito administrativo (OQ-ARCH-017): consulta PostgreSQL por cada
/// evaluación usando la identidad ya validada, sin segunda validación JWT y sin caché.
/// Si la comprobación no puede ejecutarse se aplica fail-closed: la excepción se propaga
/// como <see cref="ServiceUnavailableException"/> y la API responde 503 ProblemDetails sin
/// conceder acceso.
/// </summary>
public sealed class AdministratorAuthorizationHandler(
    IAdministratorAuthorizationRepository administratorAuthorizationRepository)
    : AuthorizationHandler<AdministratorRequirement>
{
    private const string UnavailableMessage =
        "La comprobación de habilitación administrativa no está disponible en este momento.";

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AdministratorRequirement requirement)
    {
        var userIdClaim = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId) || userId == Guid.Empty)
        {
            // Sin identidad válida el requisito no se concede; la denegación se traduce en
            // 401 (sin autenticar) o 403 (autenticado) según el resultado de autorización.
            return;
        }

        bool isAdministrator;
        try
        {
            isAdministrator = await administratorAuthorizationRepository.IsAdministratorAsync(
                userId,
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ServiceUnavailableException(UnavailableMessage, exception);
        }

        if (isAdministrator)
        {
            context.Succeed(requirement);
        }
    }
}
