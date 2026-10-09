using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.SignOut;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Identity;

/// <summary>
/// Cierre de sesión selectivo (MP-PHYS-015, escenario D): revocación por jti idempotente,
/// auditoría SIGN_OUT sin duplicados (V011, perfil N), aislamiento de password_changed_at y
/// política fail-closed ante Redis inaccesible.
/// </summary>
public class SignOutUseCaseTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakeUserCredentialRepository _userCredentialRepo = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly FakeAccessTokenDenylist _denylist = new();
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly string _secret = Convert.ToHexString(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
    private readonly Argon2idPasswordHasher _passwordHasher =
        new(new Argon2Options { MemorySize = 1024, Iterations = 2, DegreeOfParallelism = 1 });
    private readonly JwtTokenService _jwtTokenService;

    public SignOutUseCaseTests()
    {
        _jwtTokenService = new JwtTokenService(
            new JwtOptions { Secret = _secret },
            _clock);
    }

    private SignOutUseCase CreateUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _auditEventRepo,
        _jwtTokenService,
        _denylist);

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
            UserCredential.Create(
                user.Id,
                _passwordHasher.HashPassword("CorrectHorseBattery#2026"),
                passwordChangedAt ?? _clock.UtcNow.AddHours(-2)));

        return user;
    }

    private string IssueTokenFor(UserAccount user)
    {
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);
        return _jwtTokenService
            .IssueAccessToken(user.Id, user.Email.Value, credential.PasswordChangedAt)
            .Token;
    }

    private int SignOutAuditCount =>
        _auditEventRepo.Events.Count(audit => string.Equals(audit.Action, "SIGN_OUT", StringComparison.Ordinal));

    /// <summary>Firma un JWT con la misma forma que emite el backend, para variar una sola condición.</summary>
    private string CraftToken(
        Guid userId,
        string email,
        DateTime expiresAt,
        long? passwordChangedAtMicroseconds = null,
        bool includeTokenId = true)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Iat, _clock.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim(
                JwtTokenService.CredentialVersionClaim,
                (passwordChangedAtMicroseconds ?? AccessTokenRevocationPolicy.ToCredentialVersion(
                    _userCredentialRepo.Credentials.Single(c => c.UserId == userId).PasswordChangedAt))
                    .ToString(),
                ClaimValueTypes.Integer64)
        };

        if (includeTokenId)
        {
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")));
        }

        var payload = new JwtPayload(
            JwtOptions.DefaultIssuer,
            JwtOptions.DefaultAudience,
            claims,
            notBefore: null,
            expires: expiresAt);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }

    // ------------------------------------------------------------------------------------------
    // Revocación selectiva e idempotencia
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithVigentToken_ShouldRevokeThatSessionAndAuditSignOut()
    {
        // Arrange
        var user = CreateUser("signout@yusay.local");
        var token = IssueTokenFor(user);
        var tokenId = _jwtTokenService.ValidateAccessToken(token).TokenId;

        // Act
        var result = await CreateUseCase().ExecuteAsync(new SignOutCommand(token));

        // Assert: la sesión revocada es exactamente la del jti presentado
        Assert.True(result.RevokedNow);
        Assert.True(await _denylist.IsRevokedAsync(tokenId));

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("SIGN_OUT", audit.Action);
        Assert.Equal(user.Id, audit.ActorUserId);
        Assert.Equal("USER", audit.ActorKind);
        Assert.Equal("AUTHENTICATION", audit.TargetType);
        Assert.Null(audit.TargetIdentifier); // la privacidad prohíbe identificadores de sesión
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_RepeatedForTheSameToken_ShouldSucceedWithoutDuplicatingTheAudit()
    {
        // Arrange
        var user = CreateUser("idempotent@yusay.local");
        var token = IssueTokenFor(user);

        // Act: la repetición responde éxito, pero la revocación y la auditoría no se duplican
        var first = await CreateUseCase().ExecuteAsync(new SignOutCommand(token));
        var second = await CreateUseCase().ExecuteAsync(new SignOutCommand(token));

        // Assert
        Assert.True(first.RevokedNow);
        Assert.False(second.RevokedNow);
        Assert.Single(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCalledConcurrentlyForTheSameToken_ShouldAuditExactlyOnce()
    {
        // Arrange
        var user = CreateUser("concurrent@yusay.local");
        var token = IssueTokenFor(user);

        // Act: seis cierres simultáneos sobre el mismo token (semántica SET NX de la denylist)
        var tasks = Enumerable.Range(0, 6)
            .Select(_ => Task.Run(() => CreateUseCase().ExecuteAsync(new SignOutCommand(token))))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert: exactamente una llamada registra la primera revocación, que es la que audita
        Assert.Equal(1, results.Count(result => result.RevokedNow));
        Assert.Equal(6 - 1, results.Count(result => !result.RevokedNow));
        Assert.Single(_auditEventRepo.Events);
    }

    // ------------------------------------------------------------------------------------------
    // Sesiones ya inactivas: cierre idempotente sin nueva auditoría
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithExpiredToken_ShouldSucceedWithoutAuditing()
    {
        // Arrange: sesión que ya terminó por su propia expiración
        var user = CreateUser("expired@yusay.local");
        var expiredToken = CraftToken(
            user.Id,
            user.Email.Value,
            expiresAt: _clock.UtcNow.AddHours(-1).UtcDateTime);

        // Act
        var result = await CreateUseCase().ExecuteAsync(new SignOutCommand(expiredToken));

        // Assert
        Assert.False(result.RevokedNow);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(0, _denylist.RevokedCount);
    }

    [Fact]
    public async Task ExecuteAsync_AfterPasswordChange_ShouldBeIdempotentWithoutAuditingOrTouchingTheCredential()
    {
        // Arrange: la revocación global ya dejó inactiva la sesión
        var user = CreateUser("revokedglobally@yusay.local", passwordChangedAt: _clock.UtcNow.AddHours(-2));
        var oldToken = IssueTokenFor(user);
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);
        credential.ChangePassword(_passwordHasher.HashPassword("RotatedPassword#2026"), _clock.UtcNow);
        var passwordChangedAtAfterChange = credential.PasswordChangedAt;

        // Act
        var result = await CreateUseCase().ExecuteAsync(new SignOutCommand(oldToken));

        // Assert: sin revocación selectiva nueva, sin SIGN_OUT y sin alterar password_changed_at
        Assert.False(result.RevokedNow);
        Assert.Empty(_auditEventRepo.Events);
        Assert.Equal(0, _denylist.RevokedCount);
        Assert.Equal(passwordChangedAtAfterChange, credential.PasswordChangedAt);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldNotModifyPasswordChangedAt()
    {
        // Arrange
        var user = CreateUser("intact@yusay.local");
        var token = IssueTokenFor(user);
        var credential = _userCredentialRepo.Credentials.Single(c => c.UserId == user.Id);
        var passwordChangedAtBefore = credential.PasswordChangedAt;

        // Act
        await CreateUseCase().ExecuteAsync(new SignOutCommand(token));

        // Assert: la revocación selectiva no interfiere con la global por versión de credencial
        Assert.Equal(passwordChangedAtBefore, credential.PasswordChangedAt);

        // El token revocado sigue anclado a la misma versión: su rechazo proviene del jti
        Assert.True(
            await _denylist.IsRevokedAsync(_jwtTokenService.ValidateAccessToken(token).TokenId));
    }

    // ------------------------------------------------------------------------------------------
    // Rechazos
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithTamperedToken_ShouldReject()
    {
        // Arrange
        var user = CreateUser("tampered@yusay.local");
        var segments = IssueTokenFor(user).Split('.');
        var signature = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(segments[2]);
        signature[0] ^= 0xFF;
        segments[2] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(signature);
        var tamperedToken = string.Join('.', segments);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignOutCommand(tamperedToken)));
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithBlockedAccount_ShouldReject()
    {
        // Arrange
        var user = CreateUser("blocked@yusay.local");
        var token = IssueTokenFor(user);
        user.Block();

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignOutCommand(token)));
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutTokenIdClaim_ShouldReject()
    {
        // Arrange: sin jti no puede identificarse la sesión que revocar
        var user = CreateUser("nojti@yusay.local");
        var tokenWithoutJti = CraftToken(
            user.Id,
            user.Email.Value,
            expiresAt: _clock.UtcNow.AddHours(1).UtcDateTime,
            includeTokenId: false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignOutCommand(tokenWithoutJti)));
        Assert.Empty(_auditEventRepo.Events);
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyToken_ShouldThrowValidationException()
    {
        await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase().ExecuteAsync(new SignOutCommand("  ")));
        await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase().ExecuteAsync(new SignOutCommand(string.Empty)));
    }

    // ------------------------------------------------------------------------------------------
    // Fail-closed ante Redis inaccesible
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenDenylistIsUnavailable_ShouldAbortWithoutAuditing()
    {
        // Arrange
        var user = CreateUser("redisdown@yusay.local");
        var token = IssueTokenFor(user);
        _denylist.Unavailable = true;

        // Act & Assert: no se afirma una revocación que no pudo efectuarse ni se audita
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => CreateUseCase().ExecuteAsync(new SignOutCommand(token)));
        Assert.Empty(_auditEventRepo.Events);
    }

    // ------------------------------------------------------------------------------------------
    // Revocación definitiva ante fallo de auditoría (prioridad: nunca deshacer el SET NX)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WhenTheAuditFailsAfterRevoking_ShouldKeepTheRevocationAndNeverReactivateTheToken()
    {
        // Arrange: PostgreSQL falla al insertar el SIGN_OUT tras el SET NX confirmado
        var user = CreateUser("auditfail@yusay.local");
        var token = IssueTokenFor(user);
        var tokenId = _jwtTokenService.ValidateAccessToken(token).TokenId;
        _auditEventRepo.FailNextAdd = true;

        // Act & Assert: el cierre responde 503, pero la revocación confirmada NO se deshace
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => CreateUseCase().ExecuteAsync(new SignOutCommand(token)));
        Assert.True(await _denylist.IsRevokedAsync(tokenId));
        Assert.Empty(_auditEventRepo.Events);

        // Assert: la carrera concurrente tampoco reactiva el token — un segundo cierre que ya
        // observa "SET NX = false" recibe éxito sin auditar y la revocación permanece
        var concurrent = await CreateUseCase().ExecuteAsync(new SignOutCommand(token));
        Assert.False(concurrent.RevokedNow);
        Assert.Empty(_auditEventRepo.Events);
        Assert.True(await _denylist.IsRevokedAsync(tokenId));
    }
}
