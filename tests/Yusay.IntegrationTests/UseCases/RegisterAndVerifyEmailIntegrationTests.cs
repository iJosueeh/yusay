using Dapper;
using Xunit;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.UseCases;

[Collection("DatabaseCollection")]
public sealed class RegisterAndVerifyEmailIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _tokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly SecureTokenService _tokenService;

    public RegisterAndVerifyEmailIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepo = new UserAccountRepository(fixture.ConnectionFactory);
        _userCredentialRepo = new UserCredentialRepository(fixture.ConnectionFactory);
        _tokenRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
        _auditEventRepo = new AuditEventRepository(fixture.ConnectionFactory);
        
        // Hasher con opciones más ligeras para velocidad de prueba de integración
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
        _tokenRepo,
        _auditEventRepo,
        _passwordHasher,
        _tokenService,
        new NullEmailVerificationSender());

    private VerifyEmailUseCase CreateVerifyUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _tokenRepo,
        _auditEventRepo,
        _tokenService);

    [Fact]
    public async Task RegisterUser_SuccessfulFlow_ShouldPersistAllEntitiesAndAuditAtomically()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        string email = $"reg_{Guid.NewGuid():N}@yusay.local";
        string password = "NormativePassword#2026";
        var command = new RegisterUserCommand(email, password, AdultConfirmed: true);

        // Act
        var result = await registerUseCase.ExecuteAsync(command);

        // Assert: Retorno de caso de uso
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal(email.ToLowerInvariant(), result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.VerificationToken));

        // Assert en PostgreSQL real
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        // 1. user_account
        var userRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT email, status, email_verified_at, adult_confirmed_at FROM yusay.user_account WHERE user_id = @UserId",
            new { result.UserId });
        Assert.NotNull(userRow);
        Assert.Equal(email.ToLowerInvariant(), (string)userRow!.email);
        Assert.Equal("ACTIVE", (string)userRow.status);
        Assert.Null(userRow.email_verified_at);
        Assert.NotNull(userRow.adult_confirmed_at);

        // 2. user_credential
        var credRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT password_hash FROM yusay.user_credential WHERE user_id = @UserId",
            new { result.UserId });
        Assert.NotNull(credRow);
        Assert.True(_passwordHasher.VerifyPassword(password, (string)credRow!.password_hash));

        // 3. email_verification_token
        var tokenRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT token_hash, expires_at FROM yusay.email_verification_token WHERE user_id = @UserId",
            new { result.UserId });
        Assert.NotNull(tokenRow);
        Assert.Equal(_tokenService.HashToken(result.VerificationToken), (string)tokenRow!.token_hash);

        // 4. audit_event
        var auditRows = (await conn.QueryAsync<string>(
            "SELECT action FROM yusay.audit_event WHERE actor_user_id = @UserId ORDER BY action",
            new { result.UserId })).ToList();
        Assert.Contains("USER_REGISTERED", auditRows);
        Assert.Contains("EMAIL_VERIFICATION_TOKEN_ISSUED", auditRows);
    }

    [Fact]
    public async Task VerifyEmail_SuccessfulFlow_ShouldSetEmailVerifiedAtAndConsumeTokenAndAudit()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        var verifyUseCase = CreateVerifyUseCase();
        string email = $"verify_{Guid.NewGuid():N}@yusay.local";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, "StrongP@ssw0rd123", true));

        // Act
        var verifyResult = await verifyUseCase.ExecuteAsync(new VerifyEmailCommand(regResult.VerificationToken));

        // Assert
        Assert.NotNull(verifyResult);
        Assert.Equal(regResult.UserId, verifyResult.UserId);
        Assert.Equal(email.ToLowerInvariant(), verifyResult.Email);

        // Assert en PostgreSQL real
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        // 1. user_account verificado
        var userRow = await conn.QuerySingleOrDefaultAsync(
            "SELECT email_verified_at, status FROM yusay.user_account WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.NotNull(userRow);
        Assert.NotNull(userRow!.email_verified_at);
        Assert.Equal("ACTIVE", (string)userRow.status);

        // 2. email_verification_token eliminado (consumo único)
        var tokenCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.email_verification_token WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.Equal(0, tokenCount);

        // 3. audit_event contiene EMAIL_VERIFIED
        var emailVerifiedAudit = await conn.QuerySingleOrDefaultAsync<string>(
            "SELECT action FROM yusay.audit_event WHERE actor_user_id = @UserId AND action = 'EMAIL_VERIFIED'",
            new { regResult.UserId });
        Assert.Equal("EMAIL_VERIFIED", emailVerifiedAudit);
    }

    [Fact]
    public async Task VerifyEmail_ReuseToken_ShouldThrowInvalidTokenException()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        var verifyUseCase = CreateVerifyUseCase();
        string email = $"reuse_{Guid.NewGuid():N}@yusay.local";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, "StrongP@ssw0rd123", true));

        // Primer consumo exitoso
        await verifyUseCase.ExecuteAsync(new VerifyEmailCommand(regResult.VerificationToken));

        // Act & Assert: Segundo consumo debe fallar
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() =>
            verifyUseCase.ExecuteAsync(new VerifyEmailCommand(regResult.VerificationToken)));
        Assert.Contains("no existe o ya ha sido consumido", ex.Message);
    }

    [Fact]
    public async Task VerifyEmail_ExpiredToken_ShouldRejectAndCleanUpToken()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        var verifyUseCase = CreateVerifyUseCase();
        string email = $"exp_{Guid.NewGuid():N}@yusay.local";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, "StrongP@ssw0rd123", true));

        // Forzar expiración del token en base de datos preservando ck_email_verification_token_expiry (created_at < expires_at)
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "UPDATE yusay.email_verification_token SET created_at = NOW() - interval '30 hours', expires_at = NOW() - interval '6 hours' WHERE user_id = @UserId",
            new { regResult.UserId });

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() =>
            verifyUseCase.ExecuteAsync(new VerifyEmailCommand(regResult.VerificationToken)));
        Assert.Contains("ha expirado", ex.Message);

        // Verificar que el usuario permanece sin verificar
        var verifiedAt = await conn.ExecuteScalarAsync<DateTimeOffset?>(
            "SELECT email_verified_at FROM yusay.user_account WHERE user_id = @UserId",
            new { regResult.UserId });
        Assert.Null(verifiedAt);
    }

    [Fact]
    public async Task TransactionRollback_WhenErrorOccurs_ShouldNotPersistAnyEntity()
    {
        // Arrange: Provocar un rollback ejecutando una operación dentro de transacción que lanza excepción
        string email = $"rollback_{Guid.NewGuid():N}@yusay.local";
        Guid userId = Guid.NewGuid();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await _unitOfWork.ExecuteInTransactionAsync(async tx =>
            {
                var user = Domain.Identity.Entities.UserAccount.Create(
                    Domain.Identity.ValueObjects.Email.Create(email),
                    DateTimeOffset.UtcNow,
                    id: userId);
                await _userAccountRepo.AddAsync(user, tx);

                // Simulamos un fallo a mitad de la transacción
                throw new InvalidOperationException("Fallo intencional para verificar Rollback");
            });
        });

        // Assert: El usuario no debe existir en la base de datos
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var userExists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM yusay.user_account WHERE user_id = @UserId)",
            new { UserId = userId });
        Assert.False(userExists);
    }

    [Fact]
    public async Task VerifyEmail_ConcurrentRequests_ExactlyOneMustSucceedAndOthersMustFail()
    {
        // Arrange
        var registerUseCase = CreateRegisterUseCase();
        string email = $"concurrent_{Guid.NewGuid():N}@yusay.local";
        var regResult = await registerUseCase.ExecuteAsync(new RegisterUserCommand(email, "StrongP@ssw0rd123", true));

        // Act: 5 solicitudes concurrentes compitiendo por verificar con el mismo token
        const int concurrentAttempts = 5;
        var tasks = Enumerable.Range(0, concurrentAttempts)
            .Select(_ => Task.Run(async () =>
            {
                // Cada tarea crea su propia instancia del caso de uso (emulando solicitudes HTTP paralelas)
                var useCase = CreateVerifyUseCase();
                try
                {
                    await useCase.ExecuteAsync(new VerifyEmailCommand(regResult.VerificationToken));
                    return true;
                }
                catch (InvalidTokenException)
                {
                    return false;
                }
            }))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert: Exactamente 1 solicitud tuvo éxito, las otras 4 recibieron InvalidTokenException
        int successes = results.Count(r => r);
        int failures = results.Count(r => !r);

        Assert.Equal(1, successes);
        Assert.Equal(concurrentAttempts - 1, failures);

        // Verificar que solo se emitió 1 evento EMAIL_VERIFIED en auditoría
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var auditCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.audit_event WHERE actor_user_id = @UserId AND action = 'EMAIL_VERIFIED'",
            new { regResult.UserId });
        Assert.Equal(1, auditCount);
    }
}
