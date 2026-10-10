using Microsoft.AspNetCore.Authorization;

namespace Yusay.Api.Authorization;

/// <summary>
/// Requisito de autorización administrativa (OQ-ARCH-017): se concede únicamente cuando la
/// identidad ya validada dispone de una fila en yusay.administrator. La habilitación no se
/// emite en el JWT ni en ningún claim, no admite caché y la comprobación se repite en cada
/// petición; la revocación se observa en la siguiente comprobación. RN-026 intacta: la
/// política no concede acceso a respuestas, resultados ni check-ins privados ajenos.
/// </summary>
public sealed class AdministratorRequirement : IAuthorizationRequirement
{
}
