using System.Security.Claims;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Api.Common;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var claim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(claim, out var userId) || userId == Guid.Empty)
            {
                return null;
            }

            return userId;
        }
    }
}
