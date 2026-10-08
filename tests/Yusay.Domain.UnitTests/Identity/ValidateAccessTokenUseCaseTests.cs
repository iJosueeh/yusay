using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Queries.ValidateAccessToken;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Identity;

/// <summary>
/// Validación de access tokens en cada uso: firma + reglas de acceso + revocación MP-PHYS-015.
/// </summary>
public class ValidateAccessTokenUseCaseTests
{
    private const string Password = "CorrectHorseBattery#2026";

    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakeUserCredentialRepository _userCredentialRepo = new();
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly string _secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    private readonly Argon2idPasswordHasher _passwordHasher =
        new(new Argon2Options { MemorySize = 1024, Iterations = 2, DegreeOfParallelism = 1 });
    private readonly JwtTokenService _jwtTokenService;

    public ValidateAccessTokenUseCaseTests()
    {
        _jwtTokenService = new JwtTokenService(
            new JwtOptions { Secret = _secret },
            _clock);
    }

    private ValidateAccessTokenUseCase CreateUseCase() => new(
        _userAccountRepo,
        _userCredentialRepo,
        _jwtTokenService);

    private UserAccount CreateUser(
        string email,
        bool emailVerified = true,
        bool blocked = false,
        DateTimeOffset? passwordChangedAt = null)
    {
        var user = UserAccount.Create(Email.Create(email), DateTimeOffset.UtcNow);
        if (emailVerified)
        {
            user.VerifyEmail(DateTimeOffset.UtcNow);
        }

        if (blocked)
        {
            user.Block();
        }

        _userAccountRepo.Users.Add(user);
        _userCredentialRepo.Credentials.Add(
            UserCredential.Create(user.Id, _passwordHasher.HashPassword(Password), passwordChangedAt ?? _clock.UtcNow.AddHours(-2)));

        return user;
    }

    private string IssueTokenFor(UserAccount user)
    {
        // Emula a SignIn: la versión de credencial se lee de la fila junto a la contraseña.
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);
        return _jwtTokenService
            .IssueAccessToken(user.Id, user.Email.Value, credential.PasswordChangedAt)
            .Token;
    }

    private static Task<ValidateAccessTokenResult> ExecuteAsync(ValidateAccessTokenUseCase useCase, string token)
    {
        return useCase.ExecuteAsync(new ValidateAccessTokenQuery(token));
    }

    /// <summary>
    /// Instante base truncado al segundo en curso: todos los eventos simulados caen dentro de ese
    /// mismo segundo, mientras que <c>exp</c> sigue siendo futuro para el reloj real con el que el
    /// validador JWT comprueba la vigencia.
    /// </summary>
    private static DateTimeOffset CurrentSecondInstant()
    {
        return DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
    }

    /// <summary>Invierte el primer byte de la firma HMAC para simular una manipulación del token.</summary>
    private static string TamperSignature(string token)
    {
        var segments = token.Split('.');
        var signature = Base64UrlEncoder.DecodeBytes(segments[2]);
        signature[0] ^= 0xFF;
        segments[2] = Base64UrlEncoder.Encode(signature);
        return string.Join('.', segments);
    }

    /// <summary>Reescribe el correo del payload conservando la firma original.</summary>
    private static string TamperPayload(string token, string originalEmail, string forgedEmail)
    {
        var segments = token.Split('.');
        var payloadJson = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(segments[1]));
        segments[1] = Base64UrlEncoder.Encode(
            Encoding.UTF8.GetBytes(payloadJson.Replace(originalEmail, forgedEmail, StringComparison.Ordinal)));
        return string.Join('.', segments);
    }

    // ------------------------------------------------------------------------------------------
    // Token vigente
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithValidToken_ShouldReturnTheVigentIdentity()
    {
        // Arrange
        var user = CreateUser("member@yusay.local");
        var token = IssueTokenFor(user);

        // Act
        var result = await ExecuteAsync(CreateUseCase(), token);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("member@yusay.local", result.Email);
        Assert.Equal(_clock.UtcNow.ToUnixTimeSeconds(), result.IssuedAt.ToUnixTimeSeconds());
        Assert.True(result.ExpiresAt > result.IssuedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyToken_ShouldThrowValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(() => ExecuteAsync(CreateUseCase(), "   "));
        await Assert.ThrowsAsync<ValidationException>(() => ExecuteAsync(CreateUseCase(), string.Empty));
    }

    // ------------------------------------------------------------------------------------------
    // Integridad del JWT: alteración y expiración
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithAlteredPayload_ShouldReject()
    {
        // Arrange
        var user = CreateUser("member@yusay.local");
        var alteredToken = TamperPayload(IssueTokenFor(user), "member@yusay.local", "attacker@evil.test");

        // Act & Assert
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => ExecuteAsync(CreateUseCase(), alteredToken));

        Assert.Contains("no es válido", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithAlteredSignature_ShouldReject()
    {
        // Arrange
        var user = CreateUser("member@yusay.local");
        var tamperedToken = TamperSignature(IssueTokenFor(user));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), tamperedToken));
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredToken_ShouldReject()
    {
        // Arrange: mismo secreto, emisor y audiencia, con exp vencido respecto al reloj real.
        var user = CreateUser("member@yusay.local");
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            SecurityAlgorithms.HmacSha256);
        var payload = new JwtPayload(
            JwtOptions.DefaultIssuer,
            JwtOptions.DefaultAudience,
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString("D")),
                new Claim(JwtRegisteredClaimNames.Email, "member@yusay.local"),
                new Claim(
                    JwtRegisteredClaimNames.Iat,
                    DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds().ToString(),
                    ClaimValueTypes.Integer64)
            },
            notBefore: null,
            expires: DateTimeOffset.UtcNow.AddHours(-1).UtcDateTime);
        var expiredToken = new JwtSecurityTokenHandler()
            .WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), expiredToken));
    }

    // ------------------------------------------------------------------------------------------
    // Revocación tras restablecer la contraseña (MP-PHYS-015 A)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_TokenIssuedBeforePasswordReset_ShouldBeRevoked()
    {
        // Arrange: el token se emite antes del cambio de credencial
        var user = CreateUser("member@yusay.local", passwordChangedAt: _clock.UtcNow.AddHours(-2));
        var tokenIssuedBeforeReset = IssueTokenFor(user);

        // Act: la contraseña se restablece inmediatamente después de emitir el token
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);
        credential.ChangePassword(_passwordHasher.HashPassword("RotatedPassword#2026"), _clock.UtcNow);

        // Assert: el token anterior al cambio deja de ser aceptado, pese a tener firma válida
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => ExecuteAsync(CreateUseCase(), tokenIssuedBeforeReset));
        Assert.Contains("revocado", exception.Message, StringComparison.Ordinal);

        // Un token nuevo, emitido acto seguido SIN esperar a cambiar de segundo, ya es válido otra vez
        var tokenIssuedAfterReset = IssueTokenFor(user);
        var result = await ExecuteAsync(CreateUseCase(), tokenIssuedAfterReset);
        Assert.Equal(user.Id, result.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_TokenIssuedBeforeTheChangeWithinTheSameSecond_ShouldBeRejected()
    {
        // Arrange: emisión y cambio comparten segundo; el token precede al cambio por milisegundos.
        var baseInstant = CurrentSecondInstant();
        _clock.UtcNow = baseInstant.AddMilliseconds(200);
        var user = CreateUser("member@yusay.local", passwordChangedAt: baseInstant.AddMilliseconds(200));
        var tokenIssuedBefore = IssueTokenFor(user);

        // Act: el cambio ocurre en el mismo segundo, a los 800 ms
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);
        credential.ChangePassword(
            _passwordHasher.HashPassword("RotatedPassword#2026"),
            baseInstant.AddMilliseconds(800));

        // Assert: iat sería aceptado por la regla en segundos; solo la huella de versión lo veta
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), tokenIssuedBefore));
    }

    [Fact]
    public async Task ExecuteAsync_TokenIssuedAfterTheChangeWithinTheSameSecond_ShouldBeAccepted()
    {
        // Arrange: credencial cambiada a los 300 ms del segundo
        var baseInstant = CurrentSecondInstant();
        var user = CreateUser("member@yusay.local", passwordChangedAt: baseInstant);
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);
        credential.ChangePassword(
            _passwordHasher.HashPassword("RotatedPassword#2026"),
            baseInstant.AddMilliseconds(300));

        // Act: emisión dentro del mismo segundo, a los 900 ms, sin esperas artificiales
        _clock.UtcNow = baseInstant.AddMilliseconds(900);
        var tokenIssuedAfter = IssueTokenFor(user);

        // Assert: la aceptación es inmediata y determinista
        var result = await ExecuteAsync(CreateUseCase(), tokenIssuedAfter);
        Assert.Equal(user.Id, result.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_AfterTwoPasswordResetsInTheSameSecond_ShouldRejectTheIntermediateToken()
    {
        // Arrange: token emitido entre dos cambios que comparten instante
        var baseInstant = CurrentSecondInstant();
        var user = CreateUser("member@yusay.local", passwordChangedAt: baseInstant);
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);

        var firstChangeInstant = baseInstant.AddMilliseconds(100);
        credential.ChangePassword(_passwordHasher.HashPassword("RotatedPassword#2026"), firstChangeInstant);
        _clock.UtcNow = baseInstant.AddMilliseconds(300);
        var tokenBetweenResets = IssueTokenFor(user);

        // Act: segundo cambio solicitado en EL MISMO instante (reloj repetido o concurrencia), tal
        // y como lo formula ResetPasswordUseCase; la versión avanza 1 µs y queda única.
        credential.ChangePassword(
            _passwordHasher.HashPassword("RotatedPasswordTwice#2026"),
            credential.NextChangeInstant(firstChangeInstant));

        // Assert: el instante persistido es estrictamente posterior al primer cambio
        Assert.Equal(
            firstChangeInstant.AddTicks(TimeSpan.TicksPerMillisecond / 1000),
            credential.PasswordChangedAt);

        // Assert: el token intermedio queda revocado y un emitido tras el segundo cambio es válido
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), tokenBetweenResets));

        _clock.UtcNow = baseInstant.AddMilliseconds(700);
        var tokenIssuedAfterSecondReset = IssueTokenFor(user);
        var result = await ExecuteAsync(CreateUseCase(), tokenIssuedAfterSecondReset);
        Assert.Equal(user.Id, result.UserId);
    }

    [Fact]
    public async Task ExecuteAsync_TokenIssuedOneSecondAfterThePasswordChange_ShouldBeAccepted()
    {
        // Arrange
        var changeInstant = _clock.UtcNow;
        var user = CreateUser("member@yusay.local", passwordChangedAt: changeInstant);

        // Act: emisión en el primer segundo completo posterior al cambio
        _clock.UtcNow = changeInstant.AddSeconds(1);
        var token = IssueTokenFor(user);
        var result = await ExecuteAsync(CreateUseCase(), token);

        // Assert
        Assert.Equal(user.Id, result.UserId);
    }

    // ------------------------------------------------------------------------------------------
    // Reglas de acceso y cuenta suprimida
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithBlockedAccount_ShouldRejectEvenWithValidSignature()
    {
        // Arrange
        var user = CreateUser("member@yusay.local");
        var token = IssueTokenFor(user);
        user.Block();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), token));
    }

    [Fact]
    public async Task ExecuteAsync_WithUnverifiedEmail_ShouldRejectEvenWithValidSignature()
    {
        // Arrange
        var user = CreateUser("member@yusay.local", emailVerified: false);
        var token = IssueTokenFor(user);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), token));
    }

    [Fact]
    public async Task ExecuteAsync_WithDeletedAccount_ShouldRejectEvenWithValidSignature()
    {
        // Arrange
        var user = CreateUser("member@yusay.local");
        var token = IssueTokenFor(user);
        _userAccountRepo.Users.Clear();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), token));
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCredentialRecord_ShouldRejectEvenWithValidSignature()
    {
        // Arrange
        var user = CreateUser("member@yusay.local");
        var token = IssueTokenFor(user);
        _userCredentialRepo.Credentials.Clear();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), token));
    }

    [Fact]
    public async Task ExecuteAsync_RejectionsShouldAlwaysUseTheSameGenericMessage()
    {
        // Arrange: alterado, bloqueado y revocado deben ser indistinguibles para el cliente
        var activeUser = CreateUser("member@yusay.local");
        var alteredToken = TamperSignature(IssueTokenFor(activeUser));

        var blockedUser = CreateUser("blocked@yusay.local", blocked: true);
        var blockedToken = IssueTokenFor(blockedUser);

        var revokedUser = CreateUser("revoked@yusay.local");
        var revokedToken = IssueTokenFor(revokedUser);
        _userCredentialRepo.Credentials.Single(c => c.UserId == revokedUser.Id)
            .ChangePassword(_passwordHasher.HashPassword("RotatedPassword#2026"), _clock.UtcNow);

        // Act
        var altered = await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), alteredToken));
        var blocked = await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), blockedToken));
        var revoked = await Assert.ThrowsAsync<UnauthorizedException>(() => ExecuteAsync(CreateUseCase(), revokedToken));

        // Assert
        Assert.Equal(blocked.Message, altered.Message);
        Assert.Equal(revoked.Message, altered.Message);
    }
}
