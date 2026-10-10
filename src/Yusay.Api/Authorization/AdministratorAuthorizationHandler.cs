using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Api.Authorization;

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
