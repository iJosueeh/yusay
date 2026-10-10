namespace Yusay.Application.Identity.Tokens;

public static class AccessTokenRevocationPolicy
{
    private const long TicksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;

    public static long ToCredentialVersion(DateTimeOffset passwordChangedAt)
    {
        var ticksSinceEpoch = passwordChangedAt.UtcDateTime.Ticks - DateTime.UnixEpoch.Ticks;
        return ticksSinceEpoch / TicksPerMicrosecond;
    }

    public static bool IsApprovedIatRuleSatisfied(long issuedAtSeconds, DateTimeOffset passwordChangedAt)
    {
        return issuedAtSeconds >= passwordChangedAt.ToUnixTimeSeconds();
    }

    public static bool IsAccepted(
        long issuedAtSeconds,
        long issuedCredentialVersion,
        DateTimeOffset passwordChangedAt)
    {
        return IsApprovedIatRuleSatisfied(issuedAtSeconds, passwordChangedAt)
            && issuedCredentialVersion == ToCredentialVersion(passwordChangedAt);
    }
}
