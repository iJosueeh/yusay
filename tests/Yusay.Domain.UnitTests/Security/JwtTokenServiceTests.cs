using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Security;

/// <summary>
/// Emisión y validación estricta de los access tokens JWT: firma, algoritmo, emisor, audiencia,
/// vigencia e iat en segundos (MP-PHYS-015 / ADR-002).
/// </summary>
public class JwtTokenServiceTests
{
    private const string Issuer = "yusay";
    private const string Audience = "yusay-api";
    private const int LifetimeSeconds = 600;

    private readonly string _secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.UtcNow);

    private JwtOptions CreateOptions(
        string? secret = null,
        string issuer = Issuer,
        string audience = Audience,
        int lifetimeSeconds = LifetimeSeconds)
    {
        return new JwtOptions
        {
            Secret = secret ?? _secret,
            Issuer = issuer,
            Audience = audience,
            AccessTokenLifetimeSeconds = lifetimeSeconds
        };
    }

    private JwtTokenService CreateService(JwtOptions? options = null, TimeProvider? clock = null)
    {
        return new JwtTokenService(options ?? CreateOptions(), clock ?? _clock);
    }

    // ------------------------------------------------------------------------------------------
    // Emisión
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void IssueAccessToken_ThenValidate_ShouldReturnTheAuthenticatedIdentity()
    {
        // Arrange
        var service = CreateService();
        var userId = Guid.NewGuid();

        var passwordChangedAt = _clock.UtcNow.AddHours(-2);

        // Act
        var issued = service.IssueAccessToken(userId, "holder@yusay.local", passwordChangedAt);
        var validation = service.ValidateAccessToken(issued.Token);

        // Assert
        Assert.True(validation.IsValid, validation.Reason?.ToString());
        Assert.Null(validation.Reason);
        Assert.Equal(userId, validation.UserId);
        Assert.Equal("holder@yusay.local", validation.Email);
        Assert.Equal(issued.IssuedAt.ToUnixTimeSeconds(), validation.IssuedAtSeconds);
        Assert.Equal(issued.IssuedAt.ToUnixTimeSeconds(), validation.IssuedAt.ToUnixTimeSeconds());
        Assert.Equal(issued.ExpiresAt.ToUnixTimeSeconds(), validation.ExpiresAt.ToUnixTimeSeconds());
        Assert.Equal(LifetimeSeconds, validation.ExpiresAt.ToUnixTimeSeconds() - validation.IssuedAtSeconds);

        // El token conserva la huella exacta de la credencial contra la que se verificó la contraseña.
        Assert.Equal(
            AccessTokenRevocationPolicy.ToCredentialVersion(passwordChangedAt),
            validation.PasswordChangedAtMicroseconds);
    }

    [Fact]
    public void IssueAccessToken_ShouldAnchorTheTokenToTheCredentialVersionInMicroseconds()
    {
        // Arrange: password_changed_at con fracción de segundo (unidad de timestamptz).
        var service = CreateService();
        var passwordChangedAt = _clock.UtcNow;
        passwordChangedAt = passwordChangedAt.AddTicks(passwordChangedAt.Ticks % 10); // alineado a µs

        // Act
        var validation = service.ValidateAccessToken(
            service.IssueAccessToken(Guid.NewGuid(), "holder@yusay.local", passwordChangedAt).Token);

        // Assert
        Assert.True(validation.IsValid, validation.Reason?.ToString());
        Assert.Equal(
            passwordChangedAt.ToUnixTimeSeconds() * 1_000_000 + (passwordChangedAt.Ticks % TimeSpan.TicksPerSecond) / 10,
            validation.PasswordChangedAtMicroseconds);
    }

    [Fact]
    public void IssueAccessToken_ShouldSignWithHs256Only()
    {
        // Act
        var issued = CreateService().IssueAccessToken(Guid.NewGuid(), "holder@yusay.local", _clock.UtcNow);

        // Assert
        var segments = issued.Token.Split('.');
        Assert.Equal(3, segments.Length);

        var header = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(segments[0]));
        Assert.Contains("\"alg\":\"HS256\"", header, StringComparison.Ordinal);
        Assert.Contains("\"typ\":\"JWT\"", header, StringComparison.Ordinal);
    }

    [Fact]
    public void IssueAccessToken_ShouldWriteIatAsUnixSecondsNumericDate()
    {
        // Arrange
        var expectedIssuedAt = _clock.UtcNow.ToUnixTimeSeconds();

        // Act
        var issued = CreateService().IssueAccessToken(Guid.NewGuid(), "holder@yusay.local", _clock.UtcNow);

        // Assert: RFC 7519 — iat es un NumericDate entero en segundos, no una cadena ni milisegundos.
        var segments = issued.Token.Split('.');
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(segments[1]));

        Assert.Contains($"\"iat\":{expectedIssuedAt}", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("\"iat\":\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain($"{expectedIssuedAt}000", payload, StringComparison.Ordinal); // no milisegundos
    }

    [Fact]
    public void IssueAccessToken_WithoutIdentityArguments_ShouldThrow()
    {
        var service = CreateService();

        Assert.Throws<ArgumentException>(
            () => service.IssueAccessToken(Guid.Empty, "holder@yusay.local", _clock.UtcNow));
        Assert.Throws<ArgumentException>(
            () => service.IssueAccessToken(Guid.NewGuid(), "  ", _clock.UtcNow));
    }

    // ------------------------------------------------------------------------------------------
    // Firma y algoritmo
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ValidateAccessToken_WithAlteredPayload_ShouldRejectInvalidSignature()
    {
        // Arrange: se reescribe el correo del payload dejando intacta la firma original.
        var service = CreateService();
        var issued = service.IssueAccessToken(Guid.NewGuid(), "victim@yusay.local", _clock.UtcNow);

        var segments = issued.Token.Split('.');
        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(segments[1]));
        var forgedPayload = Encoding.UTF8.GetBytes(
            payloadJson.Replace("victim@yusay.local", "attacker@evil.test", StringComparison.Ordinal));
        segments[1] = Base64UrlEncoder.Encode(forgedPayload);
        var forgedToken = string.Join('.', segments);

        // Act
        var validation = service.ValidateAccessToken(forgedToken);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.InvalidSignature, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithAlteredSignature_ShouldRejectInvalidSignature()
    {
        // Arrange
        var service = CreateService();
        var issued = service.IssueAccessToken(Guid.NewGuid(), "victim@yusay.local", _clock.UtcNow);

        var segments = issued.Token.Split('.');
        var signature = Base64UrlEncoder.DecodeBytes(segments[2]);
        signature[0] ^= 0xFF;
        segments[2] = Base64UrlEncoder.Encode(signature);
        var tamperedToken = string.Join('.', segments);

        // Act
        var validation = service.ValidateAccessToken(tamperedToken);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.InvalidSignature, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithUnsignedNoneAlgorithmToken_ShouldReject()
    {
        // Arrange: token "alg: none" sin firma, el vector clásico de evasión de JWT.
        var issuedAt = _clock.UtcNow.ToUnixTimeSeconds();
        var expiresAt = issuedAt + LifetimeSeconds;
        var unsignedToken =
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}")) +
            "." +
            Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(
                $"{{\"iss\":\"{Issuer}\",\"aud\":\"{Audience}\",\"sub\":\"{Guid.NewGuid():D}\"," +
                $"\"email\":\"attacker@evil.test\",\"iat\":{issuedAt},\"exp\":{expiresAt}}}")) +
            ".";

        // Act
        var validation = CreateService().ValidateAccessToken(unsignedToken);

        // Assert
        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithRs256AlgorithmToken_ShouldReject()
    {
        // Arrange: token firmado con RS256 y clave pública propia; el algoritmo no está admitido.
        using var rsa = RSA.Create(2048);
        var credentials = new SigningCredentials(new RsaSecurityKey(rsa), SecurityAlgorithms.RsaSha256);
        var issuedAt = _clock.UtcNow.ToUnixTimeSeconds();
        var payload = new JwtPayload(
            Issuer,
            Audience,
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D")),
                new Claim(JwtRegisteredClaimNames.Email, "intruder@evil.test"),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToString(), ClaimValueTypes.Integer64)
            },
            notBefore: null,
            expires: DateTimeOffset.FromUnixTimeSeconds(issuedAt + LifetimeSeconds).UtcDateTime);
        var rsaToken = new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));

        // Act
        var validation = CreateService().ValidateAccessToken(rsaToken);

        // Assert
        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_SignedWithDifferentSecret_ShouldRejectInvalidSignature()
    {
        // Arrange
        var foreignSecret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var foreignToken = CreateService(CreateOptions(secret: foreignSecret))
            .IssueAccessToken(Guid.NewGuid(), "holder@yusay.local", _clock.UtcNow)
            .Token;

        // Act
        var validation = CreateService().ValidateAccessToken(foreignToken);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.InvalidSignature, validation.Reason);
    }

    // ------------------------------------------------------------------------------------------
    // Emisor, audiencia y vigencia
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void ValidateAccessToken_WithForeignIssuer_ShouldRejectInvalidIssuer()
    {
        // Arrange: misma clave, emisor distinto.
        var token = CraftSignedToken(issuer: "https://otro-emisor.invalid");

        // Act
        var validation = CreateService().ValidateAccessToken(token);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.InvalidIssuer, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithForeignAudience_ShouldRejectInvalidAudience()
    {
        // Arrange: misma clave, audiencia distinta.
        var token = CraftSignedToken(audience: "https://otra-audiencia.invalid");

        // Act
        var validation = CreateService().ValidateAccessToken(token);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.InvalidAudience, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithExpiredToken_ShouldRejectExpired()
    {
        // Arrange: exp vencido hace una hora, sin tolerancia de reloj.
        var expiredAt = _clock.UtcNow.AddHours(-1);
        var token = CraftSignedToken(expires: expiredAt.UtcDateTime);

        // Act
        var validation = CreateService().ValidateAccessToken(token);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.Expired, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_ExpiringExactlyNow_ShouldRejectExpired()
    {
        // Arrange: exp == ahora; la validación es estricta (ClockSkew = 0) y el instante límite es exclusivo.
        var token = CraftSignedToken(expires: _clock.UtcNow.UtcDateTime);

        // Act
        var validation = CreateService().ValidateAccessToken(token);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.Expired, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithoutIssuedAtClaim_ShouldRejectMissingIssuedAt()
    {
        // Arrange: sin iat no es posible aplicar la revocación MP-PHYS-015.
        var token = CraftSignedToken(includeIssuedAt: false);

        // Act
        var validation = CreateService().ValidateAccessToken(token);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.MissingIssuedAt, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithoutCredentialVersionClaim_ShouldRejectMissingCredentialVersion()
    {
        // Arrange: firma correcta, pero sin huella de versión no puede acreditarse vigencia
        // (MP-PHYS-015); el token se emitió fuera de este backend o fue decapitado.
        var token = CraftSignedToken(includeCredentialVersion: false);

        // Act
        var validation = CreateService().ValidateAccessToken(token);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.MissingCredentialVersion, validation.Reason);
    }

    [Fact]
    public void ValidateAccessToken_WithoutExpirationClaim_ShouldReject()
    {
        // Arrange
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            SecurityAlgorithms.HmacSha256);
        var payload = new JwtPayload(
            Issuer,
            Audience,
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString("D")),
                new Claim(JwtRegisteredClaimNames.Email, "holder@yusay.local")
            },
            notBefore: null,
            expires: null);
        var tokenWithoutExpiration = new JwtSecurityTokenHandler()
            .WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));

        // Act
        var validation = CreateService().ValidateAccessToken(tokenWithoutExpiration);

        // Assert
        Assert.False(validation.IsValid);
        Assert.NotNull(validation.Reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-es-un-jwt")]
    [InlineData("a.b.c")]
    public void ValidateAccessToken_WithMalformedInput_ShouldRejectMalformed(string? token)
    {
        // Act
        var validation = CreateService().ValidateAccessToken(token!);

        // Assert
        Assert.False(validation.IsValid);
        Assert.Equal(AccessTokenRejectionReason.Malformed, validation.Reason);
    }

    // ------------------------------------------------------------------------------------------
    // Configuración segura de la clave
    // ------------------------------------------------------------------------------------------

    [Fact]
    public void Constructor_WithoutSecret_ShouldThrowWithoutLeakingConfiguration()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => new JwtTokenService(CreateOptions(secret: string.Empty)));

        // Assert
        Assert.Contains("JWT_SECRET", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WithSecretShorterThan32Bytes_ShouldThrow()
    {
        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => new JwtTokenService(CreateOptions(secret: "segundo_corto_de_31_bytes_ok")));

        // Assert
        Assert.Contains("32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Constructor_WithNonPositiveLifetime_ShouldThrow()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(CreateOptions(lifetimeSeconds: 0)));
        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(CreateOptions(lifetimeSeconds: -60)));
    }

    // ------------------------------------------------------------------------------------------
    // Helpers de fabricación de tokens
    // ------------------------------------------------------------------------------------------

    private string CraftSignedToken(
        string? issuer = null,
        string? audience = null,
        DateTime? expires = null,
        bool includeIssuedAt = true,
        bool includeCredentialVersion = true,
        Guid? subject = null,
        string email = "holder@yusay.local")
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            SecurityAlgorithms.HmacSha256);

        var issuedAt = _clock.UtcNow;
        var claims = new List<Claim>(3)
        {
            new(JwtRegisteredClaimNames.Sub, (subject ?? Guid.NewGuid()).ToString("D")),
            new(JwtRegisteredClaimNames.Email, email)
        };

        if (includeIssuedAt)
        {
            claims.Add(new Claim(
                JwtRegisteredClaimNames.Iat,
                issuedAt.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64));
        }

        if (includeCredentialVersion)
        {
            claims.Add(new Claim(
                JwtTokenService.CredentialVersionClaim,
                AccessTokenRevocationPolicy.ToCredentialVersion(issuedAt).ToString(),
                ClaimValueTypes.Integer64));
        }

        var payload = new JwtPayload(
            issuer ?? Issuer,
            audience ?? Audience,
            claims,
            notBefore: null,
            expires: expires ?? issuedAt.AddSeconds(LifetimeSeconds).UtcDateTime);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }
}
