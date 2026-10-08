using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Identity;

/// <summary>
/// Autenticación con credenciales: Argon2id, reglas de acceso y auditoría V011 (perfiles N y F).
/// </summary>
public class SignInUseCaseTests
{
    private const string ExpectedFailureMetadata = """{"reason_code":"CREDENTIALS_NOT_ACCEPTED"}""";

    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakeUserCredentialRepository _userCredentialRepo = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly Argon2idPasswordHasher _passwordHasher =
        new(new Argon2Options { MemorySize = 1024, Iterations = 2, DegreeOfParallelism = 1 });
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.UtcNow);
    private readonly JwtTokenService _jwtTokenService;

    public SignInUseCaseTests()
    {
        _jwtTokenService = new JwtTokenService(
            new JwtOptions
            {
                Secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))
            },
            _clock);
    }

    private SignInUseCase CreateUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _auditEventRepo,
        _passwordHasher,
        _jwtTokenService);

    private UserAccount CreateUser(
        string email,
        string password,
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
            UserCredential.Create(user.Id, _passwordHasher.HashPassword(password), passwordChangedAt ?? DateTimeOffset.UtcNow.AddHours(-1)));

        return user;
    }

    // ------------------------------------------------------------------------------------------
    // Credenciales válidas
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_ShouldIssueUsableTokenAndAuditSignInSucceeded()
    {
        // Arrange
        var password = "CorrectHorseBattery#2026";
        var user = CreateUser("member@yusay.local", password);
        var useCase = CreateUseCase();

        // Act
        var result = await useCase.ExecuteAsync(new SignInCommand("member@yusay.local", password));

        // Assert: resultado de la sesión
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("member@yusay.local", result.Email);
        Assert.Equal("Bearer", result.TokenType);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal(_clock.UtcNow.ToUnixTimeSeconds(), result.IssuedAt.ToUnixTimeSeconds());
        Assert.True(result.ExpiresAt > result.IssuedAt);

        // El token emitido es validable y acredita exactamente a esa identidad
        var validation = _jwtTokenService.ValidateAccessToken(result.AccessToken);
        Assert.True(validation.IsValid, validation.Reason?.ToString());
        Assert.Equal(user.Id, validation.UserId);
        Assert.Equal("member@yusay.local", validation.Email);

        // Auditoría SIGN_IN_SUCCEEDED — perfil N: metadata ausente y destino AUTHENTICATION sin identificador
        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("SIGN_IN_SUCCEEDED", audit.Action);
        Assert.Equal("USER", audit.ActorKind);
        Assert.Equal(user.Id, audit.ActorUserId);
        Assert.Equal("AUTHENTICATION", audit.TargetType);
        Assert.Null(audit.TargetIdentifier);
        Assert.Null(audit.Metadata);
        Assert.True(_unitOfWork.TransactionExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCredentials_ShouldNotPersistTheAccessTokenInAudit()
    {
        // Arrange
        var password = "CorrectHorseBattery#2026";
        CreateUser("member@yusay.local", password);

        // Act
        var result = await CreateUseCase().ExecuteAsync(new SignInCommand("MEMBER@yusay.local", password));

        // Assert: ni el token ni el correo intentado aparecen en la auditoría
        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Null(audit.Metadata);
        Assert.DoesNotContain(result.AccessToken, audit.Metadata ?? string.Empty, StringComparison.Ordinal);
    }

    // ------------------------------------------------------------------------------------------
    // Credenciales inválidas
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithWrongPassword_ShouldRejectAndAuditSignInFailed()
    {
        // Arrange
        CreateUser("member@yusay.local", "CorrectHorseBattery#2026");

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignInCommand("member@yusay.local", "WrongPassword#2026")));

        // Assert: respuesta genérica y sin sesión emitida
        Assert.Contains("no son válidos", exception.Message, StringComparison.Ordinal);

        // Auditoría SIGN_IN_FAILED — perfil F de V011
        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("SIGN_IN_FAILED", audit.Action);
        Assert.Equal("ANONYMOUS", audit.ActorKind);
        Assert.Null(audit.ActorUserId);
        Assert.Equal("AUTHENTICATION", audit.TargetType);
        Assert.Null(audit.TargetIdentifier);
        Assert.Equal(ExpectedFailureMetadata, audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownEmail_ShouldBeIndistinguishableFromWrongPassword()
    {
        // Arrange
        CreateUser("member@yusay.local", "CorrectHorseBattery#2026");
        var useCase = CreateUseCase();

        // Act 1: cuenta inexistente
        var unknownEmailException = await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new SignInCommand("nobody@yusay.local", "AnyPassword#2026")));

        // Act 2: cuenta existente con contraseña incorrecta
        var wrongPasswordException = await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new SignInCommand("member@yusay.local", "AnyPassword#2026")));

        // Assert: mismo mensaje, mismo evento de auditoría y sin actor identificado
        Assert.Equal(wrongPasswordException.Message, unknownEmailException.Message);

        Assert.Equal(2, _auditEventRepo.Events.Count);
        Assert.All(_auditEventRepo.Events, audit =>
        {
            Assert.Equal("SIGN_IN_FAILED", audit.Action);
            Assert.Equal("ANONYMOUS", audit.ActorKind);
            Assert.Null(audit.ActorUserId);
            Assert.Null(audit.TargetIdentifier);
            Assert.Equal(ExpectedFailureMetadata, audit.Metadata);
        });
    }

    [Fact]
    public async Task ExecuteAsync_WithoutCredentialRecord_ShouldRejectAndAuditSignInFailed()
    {
        // Arrange: cuenta existente sin fila de credenciales
        var user = UserAccount.Create(Email.Create("nocreds@yusay.local"), DateTimeOffset.UtcNow);
        user.VerifyEmail(DateTimeOffset.UtcNow);
        _userAccountRepo.Users.Add(user);

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignInCommand("nocreds@yusay.local", "Whatever#2026")));

        // Assert
        Assert.Contains("no son válidos", exception.Message, StringComparison.Ordinal);
        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("SIGN_IN_FAILED", audit.Action);
        Assert.Equal(ExpectedFailureMetadata, audit.Metadata);
    }

    // ------------------------------------------------------------------------------------------
    // Reglas de acceso: BLOCKED y correo verificado
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ExecuteAsync_WithBlockedAccount_ShouldRejectDespiteValidPassword()
    {
        // Arrange: contraseña correcta, cuenta bloqueada administrativamente
        var password = "CorrectHorseBattery#2026";
        CreateUser("blocked@yusay.local", password, blocked: true);

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignInCommand("blocked@yusay.local", password)));

        // Assert
        Assert.Contains("bloqueada", exception.Message, StringComparison.Ordinal);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("SIGN_IN_FAILED", audit.Action);
        Assert.Equal("ANONYMOUS", audit.ActorKind);
        Assert.Null(audit.ActorUserId);
        Assert.Equal(ExpectedFailureMetadata, audit.Metadata); // V011 no admite otro reason_code
    }

    [Fact]
    public async Task ExecuteAsync_WithUnverifiedEmail_ShouldRejectDespiteValidPassword()
    {
        // Arrange: contraseña correcta y cuenta ACTIVE, pero correo sin verificar
        var password = "CorrectHorseBattery#2026";
        CreateUser("pending@yusay.local", password, emailVerified: false);

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignInCommand("pending@yusay.local", password)));

        // Assert
        Assert.Contains("verificarse", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("bloqueada", exception.Message, StringComparison.Ordinal);

        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("SIGN_IN_FAILED", audit.Action);
        Assert.Equal(ExpectedFailureMetadata, audit.Metadata);
    }

    [Fact]
    public async Task ExecuteAsync_WithBlockedAndUnverifiedAccount_ShouldRejectAsBlocked()
    {
        // Arrange
        var password = "CorrectHorseBattery#2026";
        CreateUser("both@yusay.local", password, emailVerified: false, blocked: true);

        // Act
        var exception = await Assert.ThrowsAsync<UnauthorizedException>(
            () => CreateUseCase().ExecuteAsync(new SignInCommand("both@yusay.local", password)));

        // Assert: el estado BLOCKED prevalece frente a la falta de verificación
        Assert.Contains("bloqueada", exception.Message, StringComparison.Ordinal);
        Assert.Equal(UserAccountStatus.Blocked, _userAccountRepo.Users.Single().Status);
    }

    // ------------------------------------------------------------------------------------------
    // Validación de entrada
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("", "AnyPassword#2026")]
    [InlineData("   ", "AnyPassword#2026")]
    [InlineData("member@yusay.local", "")]
    [InlineData("member@yusay.local", "   ")]
    public async Task ExecuteAsync_WithMissingInput_ShouldThrowValidationExceptionWithoutAuditing(string email, string password)
    {
        // Arrange
        CreateUser("member@yusay.local", "CorrectHorseBattery#2026");

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(
            () => CreateUseCase().ExecuteAsync(new SignInCommand(email, password)));

        Assert.Empty(_auditEventRepo.Events);
        Assert.False(_unitOfWork.TransactionExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidEmailFormat_ShouldRejectWithoutAuditing()
    {
        // Act & Assert
        await Assert.ThrowsAsync<Yusay.Domain.Common.DomainException>(
            () => CreateUseCase().ExecuteAsync(new SignInCommand("no-es-un-correo", "AnyPassword#2026")));

        Assert.Empty(_auditEventRepo.Events);
    }
}
