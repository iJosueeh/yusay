namespace Yusay.Application.Identity.Tokens;

public sealed record IssuedAccessToken(
    string Token,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
