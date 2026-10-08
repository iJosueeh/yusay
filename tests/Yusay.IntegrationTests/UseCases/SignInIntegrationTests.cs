using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dapper;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.RequestPasswordReset;
using Yusay.Application.Identity.Commands.ResetPassword;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Application.Identity.Queries.ValidateAccessToken;
using Yusay.Application.Identity.Tokens;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.UseCases;

/// <summary>
/// Sexta etapa de Identidad contra PostgreSQL 18: credenciales, reglas de acceso, auditoría V011,
/// integridad del JWT y revocación MP-PHYS-015 tras restablecer la contraseña.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class SignInIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly PasswordResetTokenRepository _resetTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly SecureTokenService _tokenService;
    private readonly JwtTokenService _jwtTokenService;
    private readonly string _jwtSecret = Convert.ToHexString(
        System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));

    public SignInIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepo = new UserAccountRepository(fixture.ConnectionFactory);
        _userCredentialRepo = new UserCredentialRepository(fixture.ConnectionFactory);
        _verificationTokenRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
        _resetTokenRepo = new PasswordResetTokenRepository(fixture.ConnectionFactory);
        _auditEventRepo = new AuditEventRepository(fixture.ConnectionFactory);

        _passwordHasher = new Argon2idPasswordHasher(new Argon2Options
        {
            MemorySize = 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        });
        _tokenService = new SecureTokenService();

        // Secreto de firma generado por prueba: los secretos reales viven solo en el entorno.
        _jwtTokenService = new JwtTokenService(new JwtOptions
        {
            Secret = _jwtSecret,
            AccessTokenLifetimeSeconds = 900
        });
    }

    private RegisterUserUseCase CreateRegisterUseCase() => new(
        _unitOfWork, _userAccountRepo, _userCredentialRepo, _verificationTokenRepo, _auditEventRepo, _passwordHasher, _tokenService);

    private VerifyEmailUseCase CreateVerifyEmailUseCase() => new(
        _unitOfWork, _userAccountRepo, _verificationTokenRepo, _auditEventRepo, _tokenService);

    private RequestPasswordResetUseCase CreateRequestResetUseCase() => new(
        _unitOfWork, _userAccountRepo, _resetTokenRepo, _auditEventRepo, _tokenService);

    private ResetPasswordUseCase CreateResetPasswordUseCase() => new(
        _unitOfWork, _userAccountRepo, _userCredentialRepo, _resetTokenRepo, _auditEventRepo, _passwordHasher, _tokenService);

    private SignInUseCase CreateSignInUseCase() => new(
        _unitOfWork, _userAccountRepo, _userCredentialRepo, _auditEventRepo, _passwordHasher, _jwtTokenService);

    private ValidateAccessTokenUseCase CreateValidateAccessTokenUseCase() => new(
        _userAccountRepo, _userCredentialRepo, _jwtTokenService);

    /// <summary>
    /// Lee el instante de cambio de credencial persistido, por el mismo repositorio que emplean
    /// SignIn y ValidateAccessToken (Npgsql devuelve DateTime para timestamptz vía Dapper).
    /// </summary>
    private async Task<DateTimeOffset> ReadPasswordChangedAtAsync(Guid userId)
    {
        var credential = await _userCredentialRepo.GetByUserIdAsync(userId, transaction: null, CancellationToken.None);
        Assert.NotNull(credential);
        return credential.PasswordChangedAt;
    }

    private async Task<Guid> RegisterUserAsync(string email, string password, bool verifyEmail)
    {
        var registration = await CreateRegisterUseCase()
            .ExecuteAsync(new RegisterUserCommand(email, password, AdultConfirmed: true));

        if (verifyEmail)
        {
            await CreateVerifyEmailUseCase().ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));
        }

        return registration.UserId;
    }

    // ------------------------------------------------------------------------------------------
    // Credenciales válidas
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignIn_WithValidCredentials_ShouldIssueUsableTokenAndPersistSignInSucceededAudit()
    {
        // Arrange
        string email = $"signin_ok_{Guid.NewGuid():N}@yusay.local";
        string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password, verifyEmail: true);
        var auditWindowStart = DateTimeOffset.UtcNow;

        // Act: sin esperas: el token emitido acto seguido de registrarse ya debe ser utilizable
        var result = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, password));

        // Assert: sesión emitida
        Assert.Equal(userId, result.UserId);
        Assert.Equal(email.ToLowerInvariant(), result.Email);
        Assert.Equal("Bearer", result.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.True(result.ExpiresAt > result.IssuedAt);

        // Assert: el token es utilizable contra la base de datos real (validación y revocación)
        var validated = await CreateValidateAccessTokenUseCase()
            .ExecuteAsync(new ValidateAccessTokenQuery(result.AccessToken));
        Assert.Equal(userId, validated.UserId);

        // Assert: auditoría SIGN_IN_SUCCEEDED — perfil N (V011) persistida en PostgreSQL
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var audit = await conn.QuerySingleAsync(
            """
            SELECT actor_user_id, actor_kind, action, target_type, target_identifier, metadata::text AS metadata_text
            FROM yusay.audit_event
            WHERE action = 'SIGN_IN_SUCCEEDED' AND actor_user_id = @UserId AND occurred_at >= @Since
            """,
            new { UserId = userId, Since = auditWindowStart });

        Assert.Equal("USER", (string)audit.actor_kind);
        Assert.Equal("AUTHENTICATION", (string)audit.target_type);
        Assert.Null(audit.target_identifier);
        Assert.Null(audit.metadata_text);

        // Assert: ni el access token ni el correo intentado quedan registrados en auditoría
        var leaks = await conn.ExecuteScalarAsync<int>(
            """
            SELECT count(*) FROM yusay.audit_event
            WHERE strpos(coalesce(metadata::text, ''), @Token) > 0
               OR target_identifier = @Token
               OR strpos(coalesce(target_identifier, ''), @Token) > 0
            """,
            new { Token = result.AccessToken });
        Assert.Equal(0, leaks);
    }

    // ------------------------------------------------------------------------------------------
    // Credenciales inválidas y no enumeración
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignIn_WithWrongPassword_ShouldRejectAndPersistV011CompliantSignInFailedAudit()
    {
        // Arrange
        string email = $"signin_bad_{Guid.NewGuid():N}@yusay.local";
        await RegisterUserAsync(email, "ValidPassword#2026", verifyEmail: true);
        var auditWindowStart = DateTimeOffset.UtcNow;

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, "WrongPassword#2026")));

        // Assert: respuesta genérica
        Assert.Contains("no son válidos", exception.Message, StringComparison.Ordinal);

        // Assert: auditoría SIGN_IN_FAILED — perfil F (V011)
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var audit = await conn.QuerySingleAsync(
            """
            SELECT actor_user_id, actor_kind, target_type, target_identifier, metadata::text AS metadata_text
            FROM yusay.audit_event
            WHERE action = 'SIGN_IN_FAILED' AND occurred_at >= @Since
            """,
            new { Since = auditWindowStart });

        Assert.Null(audit.actor_user_id);
        Assert.Equal("ANONYMOUS", (string)audit.actor_kind);
        Assert.Equal("AUTHENTICATION", (string)audit.target_type);
        Assert.Null(audit.target_identifier);
        Assert.Equal("""{"reason_code": "CREDENTIALS_NOT_ACCEPTED"}""", (string)audit.metadata_text);
    }

    [Fact]
    public async Task SignIn_WithUnknownEmail_ShouldBeIndistinguishableFromWrongPasswordAndStillAudit()
    {
        // Arrange
        string knownEmail = $"signin_enum_{Guid.NewGuid():N}@yusay.local";
        await RegisterUserAsync(knownEmail, "ValidPassword#2026", verifyEmail: true);
        var auditWindowStart = DateTimeOffset.UtcNow;
        string unknownEmail = $"nobody_{Guid.NewGuid():N}@yusay.local";

        // Act: cuenta inexistente y contraseña incorrecta sobre cuenta existente
        var unknownEmailException = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateSignInUseCase().ExecuteAsync(new SignInCommand(unknownEmail, "AnyPassword#2026")));
        var wrongPasswordException = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateSignInUseCase().ExecuteAsync(new SignInCommand(knownEmail, "AnyPassword#2026")));

        // Assert: misma respuesta en ambos casos
        Assert.Equal(wrongPasswordException.Message, unknownEmailException.Message);

        // Assert: dos eventos SIGN_IN_FAILED idénticos, sin actor ni identificador de destino
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var audits = (await conn.QueryAsync(
            """
            SELECT actor_user_id, actor_kind, target_identifier, metadata::text AS metadata_text
            FROM yusay.audit_event
            WHERE action = 'SIGN_IN_FAILED' AND occurred_at >= @Since
            ORDER BY occurred_at
            """,
            new { Since = auditWindowStart })).ToList();

        Assert.Equal(2, audits.Count);
        Assert.All(audits, audit =>
        {
            Assert.Null(audit.actor_user_id);
            Assert.Equal("ANONYMOUS", (string)audit.actor_kind);
            Assert.Null(audit.target_identifier);
            Assert.Equal("""{"reason_code": "CREDENTIALS_NOT_ACCEPTED"}""", (string)audit.metadata_text);
        });

        // La cuenta inexistente no fue creada ni ningún dato personal persistido en la auditoría
        var persistedUnknown = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.user_account WHERE email = @Email",
            new { Email = unknownEmail });
        Assert.Equal(0, persistedUnknown);
    }

    // ------------------------------------------------------------------------------------------
    // Reglas de acceso: BLOCKED y correo sin verificar
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task SignIn_BlockedAccount_ShouldRejectDespiteValidPasswordAndAudit()
    {
        // Arrange
        string email = $"signin_blocked_{Guid.NewGuid():N}@yusay.local";
        string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password, verifyEmail: true);

        await using (var setupConnection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await setupConnection.ExecuteAsync(
                "UPDATE yusay.user_account SET status = 'BLOCKED' WHERE user_id = @UserId",
                new { UserId = userId });
        }

        var auditWindowStart = DateTimeOffset.UtcNow;

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, password)));

        // Assert: rechazo explícito por estado de cuenta
        Assert.Contains("bloqueada", exception.Message, StringComparison.Ordinal);

        // Assert: auditoría SIGN_IN_FAILED con la única metadata admitida por V011
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var audit = await conn.QuerySingleAsync(
            """
            SELECT actor_user_id, actor_kind, target_identifier, metadata::text AS metadata_text
            FROM yusay.audit_event
            WHERE action = 'SIGN_IN_FAILED' AND occurred_at >= @Since
            """,
            new { Since = auditWindowStart });

        Assert.Null(audit.actor_user_id);
        Assert.Equal("ANONYMOUS", (string)audit.actor_kind);
        Assert.Null(audit.target_identifier);
        Assert.Equal("""{"reason_code": "CREDENTIALS_NOT_ACCEPTED"}""", (string)audit.metadata_text);
    }

    [Fact]
    public async Task SignIn_UnverifiedEmail_ShouldRejectDespiteValidPasswordAndAudit()
    {
        // Arrange: cuenta creada pero sin verificar el correo
        string email = $"signin_unverified_{Guid.NewGuid():N}@yusay.local";
        string password = "ValidPassword#2026";
        await RegisterUserAsync(email, password, verifyEmail: false);
        var auditWindowStart = DateTimeOffset.UtcNow;

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, password)));

        // Assert
        Assert.Contains("verificarse", exception.Message, StringComparison.Ordinal);

        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var failedAudits = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.audit_event WHERE action = 'SIGN_IN_FAILED' AND occurred_at >= @Since",
            new { Since = auditWindowStart });
        Assert.Equal(1, failedAudits);

        // Ningún access token fue emitido para la cuenta sin verificar
        var succeededAudits = await conn.ExecuteScalarAsync<int>(
            """
            SELECT count(*) FROM yusay.audit_event
            WHERE action = 'SIGN_IN_SUCCEEDED' AND occurred_at >= @Since
            """,
            new { Since = auditWindowStart });
        Assert.Equal(0, succeededAudits);
    }

    // ------------------------------------------------------------------------------------------
    // Integridad del JWT: alteración y expiración
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ValidateAccessToken_WithAlteredToken_ShouldReject()
    {
        // Arrange
        string email = $"signin_altered_{Guid.NewGuid():N}@yusay.local";
        string password = "ValidPassword#2026";
        await RegisterUserAsync(email, password, verifyEmail: true);

        var issued = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, password));

        // Act: se sustituye el correo del payload conservando la firma original
        var segments = issued.AccessToken.Split('.');
        var payloadJson = System.Text.Encoding.UTF8.GetString(
            Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(segments[1]));
        segments[1] = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(
            System.Text.Encoding.UTF8.GetBytes(
                payloadJson.Replace(email, $"attacker_{Guid.NewGuid():N}@evil.test", StringComparison.Ordinal)));
        var alteredToken = string.Join('.', segments);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(alteredToken)));
    }

    [Fact]
    public async Task ValidateAccessToken_WithExpiredToken_ShouldReject()
    {
        // Arrange: token artesanal con firma, emisor, audiencia, iat y huella de versión idénticos
        // a los de un token legítimo; solo exp marca la diferencia.
        string email = $"signin_expired_{Guid.NewGuid():N}@yusay.local";
        string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password, verifyEmail: true);
        var passwordChangedAt = await ReadPasswordChangedAtAsync(userId);
        var issuedAt = DateTimeOffset.UtcNow;

        long issuedAtSeconds = issuedAt.ToUnixTimeSeconds();
        long credentialVersion = AccessTokenRevocationPolicy.ToCredentialVersion(passwordChangedAt);

        // Control: el mismo token con exp futuro es aceptado
        var vigentControl = CraftToken(
            userId, email, issuedAtSeconds, credentialVersion, issuedAt.AddHours(1).UtcDateTime);
        var validated = await CreateValidateAccessTokenUseCase()
            .ExecuteAsync(new ValidateAccessTokenQuery(vigentControl));
        Assert.Equal(userId, validated.UserId);

        // Act: exp vencido, sin tolerancia de reloj
        var expiredToken = CraftToken(
            userId, email, issuedAtSeconds, credentialVersion, issuedAt.AddHours(-1).UtcDateTime);

        // Assert
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(expiredToken)));
    }

    /// <summary>
    /// Firma un JWT con el secreto de prueba y la misma forma que emite el backend, para variar una
    /// única condición por prueba.
    /// </summary>
    private string CraftToken(
        Guid userId,
        string email,
        long issuedAtSeconds,
        long passwordChangedAtMicroseconds,
        DateTime expiresAt)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_jwtSecret)),
            SecurityAlgorithms.HmacSha256);

        var payload = new JwtPayload(
            JwtOptions.DefaultIssuer,
            JwtOptions.DefaultAudience,
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAtSeconds.ToString(), ClaimValueTypes.Integer64),
                new Claim(
                    JwtTokenService.CredentialVersionClaim,
                    passwordChangedAtMicroseconds.ToString(),
                    ClaimValueTypes.Integer64)
            },
            notBefore: null,
            expires: expiresAt);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
    }

    // ------------------------------------------------------------------------------------------
    // Revocación efectiva tras restablecer la contraseña (MP-PHYS-015)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ValidateAccessToken_AfterPasswordReset_ShouldRevokePreviousTokenAndAcceptANewOneWithoutWaiting()
    {
        // Arrange
        string email = $"signin_revoke_{Guid.NewGuid():N}@yusay.local";
        string initialPassword = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, initialPassword, verifyEmail: true);

        var sessionBeforeReset = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, initialPassword));

        // El token es válido mientras la credencial no cambia
        var stillValid = await CreateValidateAccessTokenUseCase()
            .ExecuteAsync(new ValidateAccessTokenQuery(sessionBeforeReset.AccessToken));
        Assert.Equal(userId, stillValid.UserId);

        // Act: restablecimiento de contraseña (PASSWORD_RESET_COMPLETED)
        var resetRequest = await CreateRequestResetUseCase().ExecuteAsync(new RequestPasswordResetCommand(email));
        await CreateResetPasswordUseCase().ExecuteAsync(
            new ResetPasswordCommand(resetRequest.ResetToken!, "RotatedPassword#2026"));

        // Assert: el token emitido antes del cambio queda revocado, pese a su firma válida
        var revokedException = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(sessionBeforeReset.AccessToken)));
        Assert.Contains("revocado", revokedException.Message, StringComparison.Ordinal);

        // Assert: sin esperar a cambiar de segundo, la autenticación y validación inmediatas tras el
        // cambio son aceptadas (el mismo segundo que password_changed_at no es ya una ventana ciega).
        var sessionAfterReset = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, "RotatedPassword#2026"));
        var afterReset = await CreateValidateAccessTokenUseCase()
            .ExecuteAsync(new ValidateAccessTokenQuery(sessionAfterReset.AccessToken));
        Assert.Equal(userId, afterReset.UserId);

        // La antigua contraseña deja de autenticar
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, initialPassword)));
    }

    [Fact]
    public async Task ValidateAccessToken_AfterConsecutiveResets_ShouldRevokeTheTokenIssuedBetweenThem()
    {
        // Arrange
        string email = $"signin_twice_{Guid.NewGuid():N}@yusay.local";
        string initialPassword = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, initialPassword, verifyEmail: true);

        // Act: primer restablecimiento y token emitido inmediatamente después
        var firstResetRequest = await CreateRequestResetUseCase().ExecuteAsync(new RequestPasswordResetCommand(email));
        await CreateResetPasswordUseCase().ExecuteAsync(
            new ResetPasswordCommand(firstResetRequest.ResetToken!, "FirstRotated#2026"));

        var tokenBetweenResets = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, "FirstRotated#2026"));
        var validBetweenResets = await CreateValidateAccessTokenUseCase()
            .ExecuteAsync(new ValidateAccessTokenQuery(tokenBetweenResets.AccessToken));
        Assert.Equal(userId, validBetweenResets.UserId);

        // Act: segundo restablecimiento sin ninguna espera
        var secondResetRequest = await CreateRequestResetUseCase().ExecuteAsync(new RequestPasswordResetCommand(email));
        await CreateResetPasswordUseCase().ExecuteAsync(
            new ResetPasswordCommand(secondResetRequest.ResetToken!, "SecondRotated#2026"));

        // Assert: cada cambio de credencial es una versión única y revoca lo anterior a ella
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(tokenBetweenResets.AccessToken)));

        var tokenAfterBothResets = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, "SecondRotated#2026"));
        var validated = await CreateValidateAccessTokenUseCase()
            .ExecuteAsync(new ValidateAccessTokenQuery(tokenAfterBothResets.AccessToken));
        Assert.Equal(userId, validated.UserId);
    }

    [Fact]
    public async Task ValidateAccessToken_ConcurrentSignInBurstAroundPasswordReset_ShouldBehaveDeterministically()
    {
        // Arrange
        string email = $"signin_concurrent_{Guid.NewGuid():N}@yusay.local";
        string password = "ValidPassword#2026";
        var userId = await RegisterUserAsync(email, password, verifyEmail: true);

        // Act: ráfaga paralela de autenticaciones contra la misma credencial (varias instancias de
        // caso de uso y conexiones independientes, sin estado compartido en memoria)
        var burst = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
            await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, password)))));

        // Assert: toda emisión paralela es utilizable mientras la credencial no cambia
        foreach (var session in burst)
        {
            var vigent = await CreateValidateAccessTokenUseCase()
                .ExecuteAsync(new ValidateAccessTokenQuery(session.AccessToken));
            Assert.Equal(userId, vigent.UserId);
        }

        // Act: la credencial rota con la ráfaga ya emitida
        var resetRequest = await CreateRequestResetUseCase().ExecuteAsync(new RequestPasswordResetCommand(email));
        await CreateResetPasswordUseCase().ExecuteAsync(
            new ResetPasswordCommand(resetRequest.ResetToken!, "RotatedPassword#2026"));

        // Assert: revocación total e inmediata de la ráfaga, en cualquier orden de validación
        foreach (var session in burst)
        {
            await Assert.ThrowsAsync<UnauthorizedException>(
                () => CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(session.AccessToken)));
        }

        // Assert: validaciones concurrentes del token fresco dan siempre el mismo resultado
        var freshSession = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, "RotatedPassword#2026"));
        var concurrentValidations = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Task.Run(async () =>
            await CreateValidateAccessTokenUseCase().ExecuteAsync(new ValidateAccessTokenQuery(freshSession.AccessToken)))));

        Assert.All(concurrentValidations, validation => Assert.Equal(userId, validation.UserId));
    }
}
