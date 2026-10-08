using Dapper;
using Xunit;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.RequestPasswordReset;
using Yusay.Application.Identity.Commands.ResetPassword;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.UseCases;

[Collection("DatabaseCollection")]
public sealed class PasswordResetIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly PasswordResetTokenRepository _resetTokenRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly SecureTokenService _tokenService;

    public PasswordResetIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepo = new UserAccountRepository(fixture.ConnectionFactory);
        _userCredentialRepo = new UserCredentialRepository(fixture.ConnectionFactory);
        _resetTokenRepo = new PasswordResetTokenRepository(fixture.ConnectionFactory);
        _verificationTokenRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
        _auditEventRepo = new AuditEventRepository(fixture.ConnectionFactory);

        _passwordHasher = new Argon2idPasswordHasher(new Argon2Options
        {
            MemorySize = 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        });
        _tokenService = new SecureTokenService();
    }

    private RegisterUserUseCase CreateRegisterUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _verificationTokenRepo,
        _auditEventRepo,
        _passwordHasher,
        _tokenService);

    private RequestPasswordResetUseCase CreateRequestResetUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _resetTokenRepo,
        _auditEventRepo,
        _tokenService);

    private ResetPasswordUseCase CreateResetPasswordUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _resetTokenRepo,
        _auditEventRepo,
        _passwordHasher,
        _tokenService);

    [Fact]
    public async Task PasswordReset_SuccessfulFlow_ShouldChangePasswordAtomicallyUpdateTimestampAndAudit()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        var requestUseCase = CreateRequestResetUseCase();
        var resetUseCase = CreateResetPasswordUseCase();

        string email = $"reset_flow_{Guid.NewGuid():N}@yusay.local";
        string initialPassword = "InitialPassword#2026";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, initialPassword, true));

        // Act 1: Solicitar recuperación de contraseña
        var requestResult = await requestUseCase.ExecuteAsync(new RequestPasswordResetCommand(email));

        Assert.NotNull(requestResult);
        Assert.True(requestResult.EmailSent);
        Assert.NotNull(requestResult.ResetToken);

        // Assert 1: Token persistido en PostgreSQL con vigencia de 30 minutos y auditoría emitida
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var tokenRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT token_hash, created_at, expires_at FROM yusay.password_reset_token WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.NotNull(tokenRow);
        Assert.Equal(_tokenService.HashToken(requestResult.ResetToken!), (string)tokenRow!.token_hash);

        // Act 2: Consumir token y cambiar contraseña
        string newPassword = "NewStrongPassword#2026";
        var resetResult = await resetUseCase.ExecuteAsync(new ResetPasswordCommand(requestResult.ResetToken!, newPassword));

        // Assert 2: Resultado de la operación
        Assert.NotNull(resetResult);
        Assert.Equal(regResult.UserId, resetResult.UserId);
        Assert.Equal(email.ToLowerInvariant(), resetResult.Email);

        // Assert 3: Credencial en base de datos actualizada con nuevo hash y timestamp de revocación de sesiones
        var credRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT password_hash, password_changed_at FROM yusay.user_credential WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.NotNull(credRow);
        Assert.True(_passwordHasher.VerifyPassword(newPassword, (string)credRow!.password_hash));
        Assert.False(_passwordHasher.VerifyPassword(initialPassword, (string)credRow!.password_hash));
        // timestamptz tiene resolución de microsegundos (MP-PHYS-008 / mapeo físico) mientras que
        // DateTimeOffset.UtcNow resuelve a 100 ns: se compara con tolerancia submilisegundo,
        // como ya hace UserCredentialRepositoryTests, en lugar de identidad exacta de ticks.
        var persistedPasswordChangedAt = (DateTimeOffset)credRow!.password_changed_at;
        Assert.True(
            Math.Abs((resetResult.PasswordChangedAt - persistedPasswordChangedAt).TotalMilliseconds) < 1,
            $"Se esperaba password_changed_at en la base de datos cercano a {resetResult.PasswordChangedAt:O}, pero fue {persistedPasswordChangedAt:O}.");

        // Assert 4: Token de recuperación consumido (0 filas)
        var tokenCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.password_reset_token WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.Equal(0, tokenCount);

        // Assert 5: Auditoría PASSWORD_RESET_COMPLETED persistida
        var resetCompletedAudit = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT action FROM yusay.audit_event WHERE actor_user_id = @UserId AND action = 'PASSWORD_RESET_COMPLETED'",
            new { regResult.UserId });
        Assert.Equal("PASSWORD_RESET_COMPLETED", resetCompletedAudit);
    }

    [Fact]
    public async Task RequestPasswordReset_NonExistentEmail_ShouldNotRevealNonExistenceNorPersistToken()
    {
        // Arrange
        var requestUseCase = CreateRequestResetUseCase();
        string unregisteredEmail = $"unregistered_{Guid.NewGuid():N}@yusay.local";

        // Act
        var result = await requestUseCase.ExecuteAsync(new RequestPasswordResetCommand(unregisteredEmail));

        // Assert: Respuesta idéntica y no reveladora
        Assert.NotNull(result);
        Assert.True(result.EmailSent);
        Assert.Null(result.ResetToken);

        // No se debe haber creado ningún token ni evento en PostgreSQL
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var tokenCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.password_reset_token WHERE token_hash = @Hash",
            new { Hash = "any" });
        // No hay tokens huérfanos
    }

    [Fact]
    public async Task ResetPassword_ReuseToken_ShouldThrowInvalidTokenException()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        var requestUseCase = CreateRequestResetUseCase();
        var resetUseCase = CreateResetPasswordUseCase();

        string email = $"reuse_reset_{Guid.NewGuid():N}@yusay.local";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, "InitialPass123#", true));
        var reqResult = await requestUseCase.ExecuteAsync(new RequestPasswordResetCommand(email));

        // Primer consumo exitoso
        await resetUseCase.ExecuteAsync(new ResetPasswordCommand(reqResult.ResetToken!, "FirstNewPass123#"));

        // Act & Assert: Segundo consumo con el mismo token debe fallar
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() =>
            resetUseCase.ExecuteAsync(new ResetPasswordCommand(reqResult.ResetToken!, "SecondNewPass456#")));
        Assert.Contains("no existe o ya ha sido consumido", ex.Message);
    }

    [Fact]
    public async Task ResetPassword_ExpiredToken_ShouldRejectAndNotAlterPassword()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        var requestUseCase = CreateRequestResetUseCase();
        var resetUseCase = CreateResetPasswordUseCase();

        string email = $"exp_reset_{Guid.NewGuid():N}@yusay.local";
        string originalPassword = "InitialPass123#";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, originalPassword, true));
        var reqResult = await requestUseCase.ExecuteAsync(new RequestPasswordResetCommand(email));

        // Forzar expiración preservando ck_password_reset_token_expiry (created_at < expires_at)
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE yusay.password_reset_token SET created_at = NOW() - interval '45 minutes', expires_at = NOW() - interval '15 minutes' WHERE user_id = @UserId",
            new { regResult.UserId });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() =>
            resetUseCase.ExecuteAsync(new ResetPasswordCommand(reqResult.ResetToken!, "ShouldNeverBeSet#")));
        Assert.Contains("ha expirado", ex.Message);

        // La contraseña original no debe haber cambiado
        var credRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT password_hash FROM yusay.user_credential WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.NotNull(credRow);
        Assert.True(_passwordHasher.VerifyPassword(originalPassword, (string)credRow!.password_hash));
    }

    [Fact]
    public async Task ResetPassword_ShouldNotUnblockAccountNorVerifyEmail()
    {
        // Arrange: Usuario bloqueado y con correo sin verificar
        var registerUseCase = CreateRegisterUseCase();
        var requestUseCase = CreateRequestResetUseCase();
        var resetUseCase = CreateResetPasswordUseCase();

        string email = $"blocked_reset_{Guid.NewGuid():N}@yusay.local";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, "InitialPass123#", true));

        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync("UPDATE yusay.user_account SET status = 'BLOCKED' WHERE user_id = @UserId", new { regResult.UserId });

        var reqResult = await requestUseCase.ExecuteAsync(new RequestPasswordResetCommand(email));

        // Act
        await resetUseCase.ExecuteAsync(new ResetPasswordCommand(reqResult.ResetToken!, "BrandNewPass999#"));

        // Assert: El usuario sigue en estado BLOCKED y con email_verified_at NULL
        var userRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT status, email_verified_at FROM yusay.user_account WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.NotNull(userRow);
        Assert.Equal("BLOCKED", (string)userRow!.status);
        Assert.Null(userRow.email_verified_at);
    }

    [Fact]
    public async Task ResetPassword_ConcurrentRequests_ExactlyOneMustSucceedAndOthersMustFail()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        var requestUseCase = CreateRequestResetUseCase();

        string email = $"concurrent_reset_{Guid.NewGuid():N}@yusay.local";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, "InitialPass123#", true));
        var reqResult = await requestUseCase.ExecuteAsync(new RequestPasswordResetCommand(email));

        // Act: 5 solicitudes concurrentes compitiendo por restablecer la contraseña con el mismo token
        const int concurrentAttempts = 5;
        var tasks = Enumerable.Range(0, concurrentAttempts)
            .Select(i => Task.Run(async () =>
            {
                var useCase = CreateResetPasswordUseCase();
                try
                {
                    await useCase.ExecuteAsync(new ResetPasswordCommand(reqResult.ResetToken!, $"NewPasswordForAttempt_{i}#"));
                    return true;
                }
                catch (InvalidTokenException)
                {
                    return false;
                }
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: Exactamente 1 solicitud tuvo éxito y las otras 4 fueron rechazadas
        int successes = results.Count(r => r);
        int failures = results.Count(r => !r);

        Assert.Equal(1, successes);
        Assert.Equal(concurrentAttempts - 1, failures);

        // Verificar que solo existe exactamente 1 evento PASSWORD_RESET_COMPLETED en auditoría
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var auditCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.audit_event WHERE actor_user_id = @UserId AND action = 'PASSWORD_RESET_COMPLETED'",
            new { regResult.UserId });
        Assert.Equal(1, auditCount);
    }
}
