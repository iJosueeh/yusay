namespace Yusay.Api.Contracts.Identity;

/// <summary>
/// Respuesta de un inicio de sesión exitoso (<c>POST /auth/sign-in</c>). Expone únicamente
/// los datos autorizados por el contrato de autenticación: identidad pública de la cuenta y
/// credencial de sesión vigente (token de acceso, tipo, emisión y caducidad). No incluye
/// hashes de contraseña, verificadores internos ni secretos de configuración.
/// </summary>
public sealed record SignInResponse(
    Guid UserId,
    string Email,
    string AccessToken,
    string TokenType,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
