using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Sonda de pruebas SIN <c>[Authorize]</c> de clase: protección aplicada directamente a una
/// acción individual, para verificar que el esquema Bearer detecta <c>IAuthorizeData</c> a
/// nivel de acción. Definida exclusivamente en el ensamblado de pruebas — no añade
/// superficie funcional a Yusay.Api ni aparece en su documento OpenAPI.
/// </summary>
[ApiController]
[Route("test/action")]
public sealed class ActionAuthorizeProbeController : ControllerBase
{
    [HttpGet("protected")]
    [Authorize]
    public IActionResult GetProtected() => Ok(new
    {
        isAuthenticated = User.Identity?.IsAuthenticated ?? false
    });
}
