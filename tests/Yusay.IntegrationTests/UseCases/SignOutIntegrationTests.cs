using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using Xunit;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Commands.SignOut;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Application.Identity.Queries.ValidateAccessToken;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Audit.Entities;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.UseCases;

/// <summary>
/// Séptima etapa de Identidad (MP-PHYS-015, escenario D): revocación selectiva de una sesión
/// individual mediante denylist Redis compartida — selectividad, idempotencia, auditoría
/// normativa SIGN_OUT, TTL hasta la expiración del JWT, persistencia tras reinicio (AOF) y
/// política fail-closed ante Redis inaccesible.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class SignOutIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly string _jwtSecret = Convert.ToHexString(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    private IConnectionMultiplexer _redisConnection = null!;
    private RedisAccessTokenDenylist _denylist = null!;

    public SignOutIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepo = new UserAccountRepository(fixture.ConnectionFactory);
        _userCredentialRepo = new UserCredentialRepository(fixture.ConnectionFactory);
        _verificationTokenRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
        _auditEventRepo = new AuditEventRepository(fixture.ConnectionFactory);

        _passwordHasher = new Argon2idPasswordHasher(new Argon2Options
        {
            MemorySize = 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        });

        _jwtTokenService = new JwtTokenService(new JwtOptions
        {
            Secret = _jwtSecret,
            AccessTokenLifetimeSeconds = 900
        });
    }

    public async Task InitializeAsync()
    {
        await _redis.InitializeAsync();
        _redisConnection = _redis.Connect();
        _denylist = new RedisAccessTokenDenylist(_redisConnection);
    }

    public async Task DisposeAsync()
    {
        _redisConnection.Dispose();
        await _redis.DisposeAsync();
    }

    // ------------------------------------------------------------------------------------------
    // Composición de los casos de uso con la denylist Redis real
    // ------------------------------------------------------------------------------------------

    private RegisterUserUseCase CreateRegisterUseCase() => new(
        _unitOfWork, _userAccountRepo, _userCredentialRepo, _verificationTokenRepo, _auditEventRepo, _passwordHasher, new SecureTokenService(), new NullEmailVerificationSender());

    private VerifyEmailUseCase CreateVerifyEmailUseCase() => new(
        _unitOfWork, _userAccountRepo, _verificationTokenRepo, _auditEventRepo, new SecureTokenService());

    private SignInUseCase CreateSignInUseCase() => new(
        _unitOfWork, _userAccountRepo, _userCredentialRepo, _auditEventRepo, _passwordHasher, _jwtTokenService);

    private ValidateAccessTokenUseCase CreateValidateAccessTokenUseCase(
        IAccessTokenDenylist? denylist = null) => new(
        _userAccountRepo, _userCredentialRepo, _jwtTokenService, denylist ?? _denylist);

    private SignOutUseCase CreateSignOutUseCase(IAccessTokenDenylist? denylist = null) => new(
        _unitOfWork, _userAccountRepo, _userCredentialRepo, _auditEventRepo, _jwtTokenService, denylist ?? _denylist);

    private async Task<Guid> RegisterUserAsync(string email, string password)
    {
        var registration = await CreateRegisterUseCase()
            .ExecuteAsync(new RegisterUserCommand(email, password, AdultConfirmed: true));
        await CreateVerifyEmailUseCase().ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));
        return registration.UserId;
    }

    private async Task<string> SignInAsync(string email, string password)
    {
        var session = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, password));
        return session.AccessToken;
    }

    private static string ReadTokenId(string accessToken)
    {
        var claim = new JwtSecurityTokenHandler().ReadJwtToken(accessToken)
            .Claims.Single(c => string.Equals(c.Type, JwtRegisteredClaimNames.Jti, StringComparison.Ordinal));
        return claim.Value;
    }

    private async Task<long> CountSignOutAuditsAsync(Guid userId)
    {
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM yusay.audit_event WHERE action = 'SIGN_OUT' AND actor_user_id = @UserId",
            new { UserId = userId });
    }

    private async Task<DateTimeOffset> ReadPasswordChangedAtAsync(Guid userId)
    {
        var credential = await _userCredentialRepo.GetByUserIdAsync(userId, transaction: null, CancellationToken.None);
        Assert.NotNull(credential);
        return credential.PasswordChangedAt;
    }

    /// <summary>Firma un JWT con la misma forma que emite el backend, para variar una sola condición.</summary>
    private string CraftToken(
        Guid userId,
        string email,
        long passwordChangedAtMicroseconds,
        long issuedAtSeconds,
        DateTime expiresAt,
        bool includeTokenId = true)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSecret)),
            SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
            new(JwtRegisteredClaimNames.Email, email),
            new(JwtRegisteredClaimNames.Iat, issuedAtSeconds.ToString(), ClaimValueTypes.Integer64),
            new(JwtTokenService.CredentialVersionClaim, passwordChangedAtMicroseconds.ToString(), ClaimValueTypes.Integer64)
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
    // Selectividad e inmediatez
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignOut_WithVigentToken_ShouldRevokeOnlyThatSessionAndLeaveTheRestActive()
    {
        // Arrange: dos sesiones del mismo usuario
        string email = $"signout_select_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var firstSession = await SignInAsync(email, password);
        var secondSession = await SignInAsync(email, password);

        // Act: el cierre revoca únicamente la sesión presentada, de inmediato y sin esperas
        var result = await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(firstSession));

        // Assert: la sesión cerrada deja de ser aceptada
        Assert.True(result.RevokedNow);
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(firstSession)));

        // Assert: la otra sesión del mismo usuario sigue activa (revocación selectiva)
        var stillValid = await CreateValidateAccessTokenUseCase()
            .ExecuteAsync(new ValidateAccessTokenQuery(secondSession));
        Assert.Equal(userId, stillValid.UserId);
    }

    // ------------------------------------------------------------------------------------------
    // Auditoría normativa e idempotencia
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignOut_ShouldPersistNormativeSignOutAuditOnceAndStayIdempotent()
    {
        // Arrange
        string email = $"signout_audit_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);
        var tokenId = ReadTokenId(token);
        var auditWindowStart = DateTimeOffset.UtcNow;

        // Act: primera y repetición del mismo cierre
        var first = await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token));
        var second = await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token));

        // Assert: idempotente y sin duplicar la auditoría SIGN_OUT
        Assert.True(first.RevokedNow);
        Assert.False(second.RevokedNow);
        Assert.Equal(1, await CountSignOutAuditsAsync(userId));

        // Assert: perfil N de V011 — actor USER, target AUTHENTICATION, sin metadata
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var audit = await conn.QuerySingleAsync(
            """
            SELECT actor_kind, target_type, target_identifier, metadata::text AS metadata_text
            FROM yusay.audit_event
            WHERE action = 'SIGN_OUT' AND actor_user_id = @UserId AND occurred_at >= @Since
            """,
            new { UserId = userId, Since = auditWindowStart });

        Assert.Equal("USER", (string)audit.actor_kind);
        Assert.Equal("AUTHENTICATION", (string)audit.target_type);
        Assert.Null(audit.target_identifier);
        Assert.Null(audit.metadata_text);

        // Assert: el jti revocado jamás se registra en auditoría (regla de privacidad)
        var leaks = await conn.ExecuteScalarAsync<int>(
            """
            SELECT count(*) FROM yusay.audit_event
            WHERE strpos(coalesce(metadata::text, ''), @TokenId) > 0
               OR strpos(coalesce(target_identifier, ''), @TokenId) > 0
            """,
            new { TokenId = tokenId });
        Assert.Equal(0, leaks);
    }

    [Fact]
    public async Task SignOut_WhenCalledConcurrentlyForTheSameToken_ShouldAuditExactlyOnce()
    {
        // Arrange
        string email = $"signout_race_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);

        // Act: ráfaga concurrente de cierres sobre el mismo token (SET NX en Redis)
        var tasks = Enumerable.Range(0, 6)
            .Select(_ => Task.Run(() => CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token))))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert: todos responden éxito (idempotencia), pero solo una revocación es "nueva"
        Assert.Equal(1, results.Count(result => result.RevokedNow));
        Assert.Equal(5, results.Count(result => !result.RevokedNow));
        Assert.Equal(1, await CountSignOutAuditsAsync(userId));
    }

    // ------------------------------------------------------------------------------------------
    // TTL hasta la expiración efectiva del JWT
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignOut_ShouldExpireTheRevocationKeyExactlyWithTheJwt()
    {
        // Arrange
        string email = $"signout_ttl_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);
        var tokenId = ReadTokenId(token);
        var expiresAt = new DateTimeOffset(
            new JwtSecurityTokenHandler().ReadJwtToken(token).ValidTo,
            TimeSpan.Zero);

        // Act
        await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token));

        // Assert: la clave vive exactamente hasta la expiración del token (no más, no menos)
        var timeToLive = await _redisConnection.GetDatabase()
            .KeyTimeToLiveAsync(RedisAccessTokenDenylist.KeyFor(tokenId));

        Assert.NotNull(timeToLive);
        var remainingLifetime = expiresAt - DateTimeOffset.UtcNow;
        Assert.True(
            timeToLive.Value <= remainingLifetime + TimeSpan.FromSeconds(2),
            $"TTL {timeToLive} excede la vida restante del JWT {remainingLifetime}");
        Assert.True(
            timeToLive.Value >= remainingLifetime - TimeSpan.FromSeconds(5),
            $"TTL {timeToLive} vence antes que el JWT {remainingLifetime}");
    }

    // ------------------------------------------------------------------------------------------
    // Persistencia tras reinicio (AOF) y fallo de Redis (fail-closed)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignOut_RevocationShouldSurviveARedisRestart()
    {
        // Arrange
        string email = $"signout_restart_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);
        var tokenId = ReadTokenId(token);

        await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token));

        // Act: reinicio del servicio con AOF (mismo volumen, como en docker-compose)
        await _redis.RestartAsync();

        // El cliente pierde su socket durante el reinicio y, hasta que la sesión TCP vuelva,
        // sus comandos caducan en el backlog (fail-closed); para comprobar la persistencia de la
        // clave AOF se abre una conexión nueva, como haría cualquier instancia al recuperarse.
        _redisConnection.Dispose();
        _redisConnection = _redis.Connect(timeoutMilliseconds: 10000);
        _denylist = new RedisAccessTokenDenylist(_redisConnection);

        // Assert: la revocación sobrevive al reinicio y el token sigue bloqueado
        Assert.True(await _denylist.IsRevokedAsync(tokenId));
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(token)));
    }

    [Fact]
    public async Task SignOut_ShouldFailClosedWhileRedisIsStoppedAndRecoverWithTheSameClient()
    {
        // Arrange
        string email = $"signout_outage_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);
        var tokenId = ReadTokenId(token);

        await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token));

        // Act: interrupción del servicio — el multiplexer compartido NO se reconecta manualmente
        await _redis.StopAsync();
        try
        {
            // Assert: durante la interrupción la validación protegida falla en modo fail-closed
            await Assert.ThrowsAsync<ServiceUnavailableException>(
                () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(token)));

            // Assert: el cierre también se aborta y no produce una segunda auditoría
            await Assert.ThrowsAsync<ServiceUnavailableException>(
                () => CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token)));
            Assert.Equal(1, await CountSignOutAuditsAsync(userId));
        }
        finally
        {
            await _redis.StartAsync();
        }

        // Assert: el mismo cliente se reconecta solo y, recuperado, vuelve a operar
        var recovered = false;
        string? lastError = null;
        for (var attempt = 0; attempt < 10 && !recovered; attempt++)
        {
            try
            {
                recovered = await _denylist.IsRevokedAsync(tokenId);
            }
            catch (ServiceUnavailableException ex)
            {
                lastError = ex.InnerException?.Message.Split('\n')[0];
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        Assert.True(
            recovered,
            $"El multiplexer no se reconectó a Redis tras la recuperación del servicio. Último error: {lastError}{Environment.NewLine}" +
            ((ConnectionMultiplexer)_redisConnection).GetStatus());

        // Y la validación vuelve a rechazar por la revocación persistida (AOF), no por indisponibilidad
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(token)));
    }

    [Fact]
    public async Task SignOut_WhenTheAuditPersistFails_ShouldKeepTheRevocationAndNeverReactivateTheToken()
    {
        // Arrange: PostgreSQL falla al insertar el SIGN_OUT tras el SET NX confirmado en Redis
        string email = $"signout_auditfail_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);
        var tokenId = ReadTokenId(token);
        var failingAuditRepository = new FailingAuditEventRepository(_auditEventRepo);

        // Act & Assert: el cierre responde 503 (limitación de completitud), nunca "éxito sin auditoría"
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => new SignOutUseCase(
                    _unitOfWork,
                    _userAccountRepo,
                    _userCredentialRepo,
                    failingAuditRepository,
                    _jwtTokenService,
                    _denylist)
                .ExecuteAsync(new SignOutCommand(token)));

        // Assert: la revocación confirmada permanece en Redis real y la sesión NUNCA se reactiva
        Assert.True(await _denylist.IsRevokedAsync(tokenId));
        Assert.Equal(0, await CountSignOutAuditsAsync(userId));
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(token)));

        // Assert: el reintento confirma el cierre sin duplicar la auditoría — y, como limitación
        // documentada (MP-PHYS-015 §D), no regenera el SIGN_OUT que no pudo persistirse
        var retry = await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token));
        Assert.False(retry.RevokedNow);
        Assert.Equal(0, await CountSignOutAuditsAsync(userId));
        Assert.True(await _denylist.IsRevokedAsync(tokenId));
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(token)));
    }

    [Fact]
    public async Task ValidateAccessTokenAndSignOut_WhenRedisIsUnreachable_ShouldFailClosed()
    {
        // Arrange
        string email = $"signout_down_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);

        using var unreachableRedis = RedisFixture.ConnectToUnreachableEndpoint();
        var failingDenylist = new RedisAccessTokenDenylist(unreachableRedis);

        // Act & Assert: la validación protegida no acepta el token sin poder consultar la denylist
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => CreateValidateAccessTokenUseCase(failingDenylist)
                .ExecuteAsync(new ValidateAccessTokenQuery(token)));

        // Act & Assert: el cierre se aborta sin auditar una revocación que no pudo efectuarse
        await Assert.ThrowsAsync<ServiceUnavailableException>(
            () => CreateSignOutUseCase(failingDenylist).ExecuteAsync(new SignOutCommand(token)));
        Assert.Equal(0, await CountSignOutAuditsAsync(userId));
    }

    // ------------------------------------------------------------------------------------------
    // Aislamiento de password_changed_at y rechazos de sesión
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignOut_ShouldNotModifyPasswordChangedAt()
    {
        // Arrange
        string email = $"signout_intact_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var token = await SignInAsync(email, password);
        var passwordChangedAtBefore = await ReadPasswordChangedAtAsync(userId);

        // Act
        await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(token));

        // Assert: la revocación selectiva no interfiere con la revocación global por credencial
        Assert.Equal(passwordChangedAtBefore, await ReadPasswordChangedAtAsync(userId));

        // Y el token sigue anclado a su versión: su rechazo proviene del jti, no del pwd_at
        var credential = await _userCredentialRepo.GetByUserIdAsync(userId, transaction: null, CancellationToken.None);
        Assert.NotNull(credential);
        var validation = _jwtTokenService.ValidateAccessToken(token);
        Assert.True(validation.IsValid, validation.Reason?.ToString());
        Assert.Equal(
            AccessTokenRevocationPolicy.ToCredentialVersion(passwordChangedAtBefore),
            validation.PasswordChangedAtMicroseconds);
    }

    [Fact]
    public async Task SignOut_WithTokenWithoutJti_ShouldReject()
    {
        // Arrange: token firmado por el backend pero sin identidad de sesión que revocar
        string email = $"signout_nojti_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var now = DateTimeOffset.UtcNow;
        var craftedWithoutTokenId = CraftToken(
            userId,
            email,
            passwordChangedAtMicroseconds: AccessTokenRevocationPolicy.ToCredentialVersion(
                await ReadPasswordChangedAtAsync(userId)),
            issuedAtSeconds: now.ToUnixTimeSeconds(),
            expiresAt: now.AddHours(1).UtcDateTime,
            includeTokenId: false);

        // Act & Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(craftedWithoutTokenId)));
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(craftedWithoutTokenId)));
        Assert.Equal(0, await CountSignOutAuditsAsync(userId));
    }

    [Fact]
    public async Task SignOut_WithExpiredToken_ShouldSucceedWithoutAuditing()
    {
        // Arrange: la sesión ya terminó por su propia expiración
        string email = $"signout_exp_{Guid.NewGuid():N}@yusay.local";
        const string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password);
        var now = DateTimeOffset.UtcNow;
        var expiredToken = CraftToken(
            userId,
            email,
            passwordChangedAtMicroseconds: AccessTokenRevocationPolicy.ToCredentialVersion(
                await ReadPasswordChangedAtAsync(userId)),
            issuedAtSeconds: now.AddHours(-2).ToUnixTimeSeconds(),
            expiresAt: now.AddHours(-1).UtcDateTime);

        // Act
        var result = await CreateSignOutUseCase().ExecuteAsync(new SignOutCommand(expiredToken));

        // Assert: cierre idempotente, sin revocación selectiva nueva ni SIGN_OUT
        Assert.False(result.RevokedNow);
        Assert.Equal(0, await CountSignOutAuditsAsync(userId));
    }

    /// <summary>Repositorio de auditoría que inyecta un fallo de persistencia (PostgreSQL caído).</summary>
    private sealed class FailingAuditEventRepository(IAuditEventRepository inner) : IAuditEventRepository
    {
        private int _pendingFailures = 1;

        public Task AddAsync(AuditEvent auditEvent, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref _pendingFailures) >= 0)
            {
                throw new InvalidOperationException("Fallo inyectado de PostgreSQL al registrar SIGN_OUT.");
            }

            return inner.AddAsync(auditEvent, transaction, cancellationToken);
        }
    }
}
