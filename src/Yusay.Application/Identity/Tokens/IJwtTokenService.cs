namespace Yusay.Application.Identity.Tokens;

public interface IJwtTokenService
{
    IssuedAccessToken IssueAccessToken(Guid userId, string email, DateTimeOffset passwordChangedAt);
    AccessTokenValidationResult ValidateAccessToken(string accessToken);
}
