namespace Yusay.Infrastructure.Identity.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public const int MinimumSecretLengthBytes = 32;
    public const string DefaultIssuer = "yusay";
    public const string DefaultAudience = "yusay-api";
    public const int DefaultAccessTokenLifetimeSeconds = 3600;
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = DefaultIssuer;
    public string Audience { get; set; } = DefaultAudience;
    public int AccessTokenLifetimeSeconds { get; set; } = DefaultAccessTokenLifetimeSeconds;
}
