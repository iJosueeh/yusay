using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Yusay.Application.Common.Interfaces;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Sonda de pruebas de <see cref="ICurrentUser"/> (F1 de N1): identidad corriente expuesta en
/// un endpoint protegido con <c>[Authorize]</c> y en un endpoint anónimo sin protección.
/// Definida exclusivamente en el ensamblado de pruebas — no añade superficie funcional a
/// Yusay.Api ni aparece en su documento OpenAPI.
/// </summary>
[ApiController]
[Route("test/current-user")]
public sealed class CurrentUserProbeController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("protected")]
    [Authorize]
    public IActionResult GetProtected() => Ok(new { userId = currentUser.UserId });

    [HttpGet("anonymous")]
    public IActionResult GetAnonymous() => Ok(new { userId = currentUser.UserId });
}
