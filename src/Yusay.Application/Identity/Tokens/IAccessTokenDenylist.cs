namespace Yusay.Application.Identity.Tokens;

public interface IAccessTokenDenylist
{
    Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task<bool> RevokeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);
}
