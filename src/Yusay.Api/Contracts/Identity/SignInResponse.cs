namespace Yusay.Api.Contracts.Identity;

public sealed record SignInResponse(
    Guid UserId,
    string Email,
    string AccessToken,
    string TokenType,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
