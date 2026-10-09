namespace Yusay.Api.Contracts.Identity;

/// <summary>Solicitud de inicio de sesión (<c>POST /auth/sign-in</c>).</summary>
public sealed record SignInRequest(string Email, string Password);
