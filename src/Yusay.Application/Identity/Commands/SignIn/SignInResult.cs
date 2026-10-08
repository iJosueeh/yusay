namespace Yusay.Application.Identity.Commands.SignIn;

public sealed record SignInResult(
    Guid UserId,
    string Email,
    string AccessToken,
    string TokenType,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
