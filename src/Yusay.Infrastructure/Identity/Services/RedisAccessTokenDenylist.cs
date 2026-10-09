using StackExchange.Redis;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Tokens;

namespace Yusay.Infrastructure.Identity.Services;

public sealed class RedisAccessTokenDenylist(IConnectionMultiplexer redis, TimeProvider? timeProvider = null) : IAccessTokenDenylist
{
    public const string KeyPrefix = "yusay:access_token:revoked:";

    private const string UnavailableMessage = "El almacén de revocación de sesiones no está disponible.";

    private readonly IConnectionMultiplexer _redis = redis ?? throw new ArgumentNullException(nameof(redis));
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public static string KeyFor(string tokenId) => KeyPrefix + tokenId;

    public async Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        try
        {
            return await _redis.GetDatabase().KeyExistsAsync(KeyFor(tokenId));
        }
        catch (Exception exception) when (IsInfrastructureFailure(exception))
        {
            throw new ServiceUnavailableException(UnavailableMessage, exception);
        }
    }

    public async Task<bool> RevokeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        var remainingLifetime = expiresAt - _timeProvider.GetUtcNow();
        if (remainingLifetime <= TimeSpan.Zero)
        {
            return false;
        }

        try
        {
            return await _redis.GetDatabase().StringSetAsync(
                KeyFor(tokenId),
                _timeProvider.GetUtcNow().ToUnixTimeSeconds(),
                remainingLifetime,
                When.NotExists);
        }
        catch (Exception exception) when (IsInfrastructureFailure(exception))
        {
            throw new ServiceUnavailableException(UnavailableMessage, exception);
        }
    }

    private static bool IsInfrastructureFailure(Exception exception)
    {
        return exception is RedisException
            or TimeoutException
            or ObjectDisposedException
            or InvalidOperationException;
    }
}
