using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Yusay.Application.Identity.Tokens;

namespace Yusay.Infrastructure.Identity.Services;

public sealed class JwtTokenService : IJwtTokenService
{
    /// <summary>
    /// Claim que ancla el token a la versión exacta de la credencial (microsegundos Unix de
    /// <c>yusay.user_credential.password_changed_at</c>) contra la que se verificó la contraseña.
    /// Sin él el token no puede demostrar que sigue vigente y se rechaza (MP-PHYS-015).
    /// </summary>
    public const string CredentialVersionClaim = "pwd_at";

    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public JwtTokenService(JwtOptions options, TimeProvider? timeProvider = null)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _timeProvider = timeProvider ?? TimeProvider.System;

        ValidateOptions(_options);

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Secret));
        _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        _validationParameters = new TokenValidationParameters
        {
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,

            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,

            ValidateLifetime = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.Zero
        };

        _tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };
    }

    public IssuedAccessToken IssueAccessToken(Guid userId, string email, DateTimeOffset passwordChangedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("El identificador de usuario no puede ser un UUID vacío.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("El correo electrónico es obligatorio para emitir un token.", nameof(email));
        }

        var issuedAt = _timeProvider.GetUtcNow();
        var expiresAt = issuedAt.AddSeconds(_options.AccessTokenLifetimeSeconds);
        var issuedAtSeconds = issuedAt.ToUnixTimeSeconds();

        var claims = new List<Claim>(5)
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),

            new(JwtRegisteredClaimNames.Iat, issuedAtSeconds.ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),

            // Versión de credencial leída de la fila validada: si la contraseña cambia después de
            // esta lectura, el token nace revocado aunque su firma y su iat sigan siendo correctos.
            new(
                CredentialVersionClaim,
                AccessTokenRevocationPolicy.ToCredentialVersion(passwordChangedAt).ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        var header = new JwtHeader(_signingCredentials);
        var payload = new JwtPayload(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: null,
            expires: expiresAt.UtcDateTime);

        var securityToken = new JwtSecurityToken(header, payload);
        var encodedToken = _tokenHandler.WriteToken(securityToken);

        return new IssuedAccessToken(encodedToken, issuedAt, expiresAt);
    }

    public AccessTokenValidationResult ValidateAccessToken(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return AccessTokenValidationResult.Reject(AccessTokenRejectionReason.Malformed);
        }

        JwtSecurityToken securityToken;
        try
        {
            _tokenHandler.ValidateToken(accessToken, _validationParameters, out var validatedToken);
            securityToken = validatedToken as JwtSecurityToken
                ?? throw new SecurityTokenMalformedException("El token validado no es un JWT.");
        }
        catch (Exception exception) when (
            exception is SecurityTokenException or ArgumentException or FormatException or NotSupportedException)
        {
            return AccessTokenValidationResult.Reject(MapRejectionReason(exception));
        }

        if (!TryReadUnixSeconds(securityToken, JwtRegisteredClaimNames.Iat, out var issuedAtSeconds))
        {
            return AccessTokenValidationResult.Reject(AccessTokenRejectionReason.MissingIssuedAt);
        }

        if (!TryReadUnixSeconds(securityToken, "exp", out var expiresAtSeconds))
        {
            return AccessTokenValidationResult.Reject(AccessTokenRejectionReason.Malformed);
        }

        if (!TryReadUnixSeconds(securityToken, CredentialVersionClaim, out var passwordChangedAtMicroseconds))
        {
            return AccessTokenValidationResult.Reject(AccessTokenRejectionReason.MissingCredentialVersion);
        }

        var subject = ReadClaim(securityToken, JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var userId) || userId == Guid.Empty)
        {
            return AccessTokenValidationResult.Reject(AccessTokenRejectionReason.Malformed);
        }

        var email = ReadClaim(securityToken, JwtRegisteredClaimNames.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            return AccessTokenValidationResult.Reject(AccessTokenRejectionReason.Malformed);
        }

        return new AccessTokenValidationResult(
            IsValid: true,
            UserId: userId,
            Email: email,
            IssuedAtSeconds: issuedAtSeconds,
            PasswordChangedAtMicroseconds: passwordChangedAtMicroseconds,
            IssuedAt: DateTimeOffset.FromUnixTimeSeconds(issuedAtSeconds),
            ExpiresAt: DateTimeOffset.FromUnixTimeSeconds(expiresAtSeconds),
            Reason: null);
    }

    private static void ValidateOptions(JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Secret))
        {
            throw new InvalidOperationException(
                "JWT_SECRET no está definido. Configura la variable de entorno JWT_SECRET " +
                $"(mínimo {JwtOptions.MinimumSecretLengthBytes} bytes) fuera del repositorio.");
        }

        if (Encoding.UTF8.GetByteCount(options.Secret) < JwtOptions.MinimumSecretLengthBytes)
        {
            throw new InvalidOperationException(
                $"JWT_SECRET debe contener al menos {JwtOptions.MinimumSecretLengthBytes} bytes para firmar con HS256.");
        }

        if (string.IsNullOrWhiteSpace(options.Issuer))
        {
            throw new InvalidOperationException("JWT_ISSUER no puede estar vacío.");
        }

        if (string.IsNullOrWhiteSpace(options.Audience))
        {
            throw new InvalidOperationException("JWT_AUDIENCE no puede estar vacío.");
        }

        if (options.AccessTokenLifetimeSeconds <= 0)
        {
            throw new InvalidOperationException("JWT_ACCESS_TOKEN_TTL_SECONDS debe ser un entero positivo.");
        }
    }

    private static AccessTokenRejectionReason MapRejectionReason(Exception exception)
    {
        return exception switch
        {
            SecurityTokenInvalidAlgorithmException => AccessTokenRejectionReason.InvalidAlgorithm,
            SecurityTokenSignatureKeyNotFoundException => AccessTokenRejectionReason.InvalidSignature,
            SecurityTokenInvalidSignatureException => AccessTokenRejectionReason.InvalidSignature,
            SecurityTokenInvalidIssuerException => AccessTokenRejectionReason.InvalidIssuer,
            SecurityTokenInvalidAudienceException => AccessTokenRejectionReason.InvalidAudience,
            SecurityTokenExpiredException => AccessTokenRejectionReason.Expired,
            SecurityTokenNoExpirationException => AccessTokenRejectionReason.Expired,
            SecurityTokenInvalidLifetimeException => AccessTokenRejectionReason.Expired,
            _ => AccessTokenRejectionReason.Malformed
        };
    }

    private static string? ReadClaim(JwtSecurityToken token, string claimType)
    {
        foreach (var claim in token.Payload.Claims)
        {
            if (string.Equals(claim.Type, claimType, StringComparison.Ordinal))
            {
                return claim.Value;
            }
        }

        return null;
    }

    private static bool TryReadUnixSeconds(JwtSecurityToken token, string claimType, out long seconds)
    {
        seconds = 0;
        var raw = ReadClaim(token, claimType);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
        {
            return true;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var fractionalSeconds))
        {
            seconds = (long)fractionalSeconds;
            return true;
        }

        return false;
    }
}
