namespace Yusay.Application.Identity.Tokens;

public sealed record AccessTokenValidationResult(
    bool IsValid,
    Guid UserId,
    string Email,
    string TokenId,
    long IssuedAtSeconds,
    long PasswordChangedAtMicroseconds,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    AccessTokenRejectionReason? Reason)
{
    public static AccessTokenValidationResult Reject(AccessTokenRejectionReason reason)
    {
        return new AccessTokenValidationResult(
            IsValid: false,
            UserId: Guid.Empty,
            Email: string.Empty,
            TokenId: string.Empty,
            IssuedAtSeconds: 0,
            PasswordChangedAtMicroseconds: 0,
            IssuedAt: DateTimeOffset.MinValue,
            ExpiresAt: DateTimeOffset.MinValue,
            Reason: reason);
    }
}
