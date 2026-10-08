namespace Yusay.Application.Identity.Queries.ValidateAccessToken;

public sealed record ValidateAccessTokenResult(
    Guid UserId,
    string Email,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
