using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Endpoint protegido SOLO para pruebas, definido en el ensamblado de pruebas para ejercitar
/// el esquema Bearer sin añadir superficie funcional a Yusay.Api: el documento OpenAPI real se
/// genera desde el ensamblado de la API y esta ruta no aparece en él.
/// </summary>
[ApiController]
[Route("test/protected")]
[Authorize]
public sealed class ProtectedProbeController : ControllerBase
{
    /// <summary>Requiere un JWT validado por IValidateAccessTokenUseCase; expone la identidad
    /// del ClaimsPrincipal construido a partir de esa validación.</summary>
    [HttpGet]
    public IActionResult Get() => Ok(new
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier),
        email = User.FindFirstValue(ClaimTypes.Name),
        isAuthenticated = User.Identity?.IsAuthenticated ?? false
    });

    /// <summary>AllowAnonymous dentro de un controlador protegido: debe responder sin token.</summary>
    [HttpGet("anonymous")]
    [AllowAnonymous]
    public IActionResult GetAnonymous() => Ok(new
    {
        isAuthenticated = User.Identity?.IsAuthenticated ?? false
    });
}
