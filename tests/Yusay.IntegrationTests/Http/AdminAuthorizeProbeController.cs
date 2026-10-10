using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Api.Authorization;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Sonda de pruebas de la política Administrator, definida exclusivamente en el ensamblado
/// de pruebas: la protección se aplica por política a toda la clase y una acción
/// [AllowAnonymous] verifica la precedencia del anonimato. No añade superficie funcional a
/// Yusay.Api ni aparece en su documento OpenAPI.
/// </summary>
[ApiController]
[Route("test/admin")]
[Authorize(Policy = AdministratorPolicy.Name)]
public sealed class AdminAuthorizeProbeController : ControllerBase
{
    /// <summary>Requiere JWT validado y fila en yusay.administrator.</summary>
    [HttpGet("protected")]
    public IActionResult GetProtected() => Ok(new
    {
        isAdministrator = true,
        userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)
    });

    /// <summary>AllowAnonymous con precedencia sobre la política Administrator de clase.</summary>
    [HttpGet("anonymous")]
    [AllowAnonymous]
    public IActionResult GetAnonymous() => Ok(new
    {
        isAuthenticated = User.Identity?.IsAuthenticated ?? false
    });
}
