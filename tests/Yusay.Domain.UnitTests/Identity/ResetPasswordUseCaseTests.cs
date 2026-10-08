using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.ResetPassword;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Identity;

public class ResetPasswordUseCaseTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakeUserCredentialRepository _userCredentialRepo = new();
    private readonly FakePasswordResetTokenRepository _resetTokenRepo = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly Argon2idPasswordHasher _passwordHasher = new(new Argon2Options { MemorySize = 1024, Iterations = 2, DegreeOfParallelism = 1 });
    private readonly SecureTokenService _tokenService = new();

    private ResetPasswordUseCase CreateUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _resetTokenRepo,
        _auditEventRepo,
        _passwordHasher,
        _tokenService);

    [Fact]
    public async Task ExecuteAsync_WithValidToken_ShouldChangePasswordUpdateTimestampConsumeTokenAndAudit()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create("resetpass@yusay.org"), DateTimeOffset.UtcNow);
        _userAccountRepo.Users.Add(user);

        var originalPasswordHash = _passwordHasher.HashPassword("OldPassword123!");
        var initialChangedAt = DateTimeOffset.UtcNow.AddDays(-10);
        var credential = UserCredential.Create(user.Id, originalPasswordHash, initialChangedAt);
        _userCredentialRepo.Credentials.Add(credential);

        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);
        var resetToken = PasswordResetToken.Create(user.Id, tokenHash, DateTimeOffset.UtcNow);
        _resetTokenRepo.Tokens.Add(resetToken);

        var useCase = CreateUseCase();
        string newPassword = "BrandNewSecurePassword456#";
        var command = new ResetPasswordCommand(rawToken, newPassword);

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("resetpass@yusay.org", result.Email);

        // Credencial actualizada
        Assert.True(_passwordHasher.VerifyPassword(newPassword, credential.PasswordHash));
        Assert.False(_passwordHasher.VerifyPassword("OldPassword123!", credential.PasswordHash));
        Assert.True(credential.PasswordChangedAt > initialChangedAt);
        Assert.Equal(result.PasswordChangedAt, credential.PasswordChangedAt);

        // Consumo único: token eliminado de la base de datos
        Assert.Empty(_resetTokenRepo.Tokens);

        // Auditoría PASSWORD_RESET_COMPLETED
        Assert.Single(_auditEventRepo.Events);
        var audit = _auditEventRepo.Events.First();
        Assert.Equal("PASSWORD_RESET_COMPLETED", audit.Action);
        Assert.Equal(user.Id, audit.ActorUserId);

        Assert.True(_unitOfWork.TransactionExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveBlockedStatusAndEmailVerifiedAt()
    {
        // Arrange: Usuario bloqueado y con correo sin verificar
        var user = UserAccount.Create(Email.Create("blocked@yusay.org"), DateTimeOffset.UtcNow);
        user.Block(); // Status = Blocked
        _userAccountRepo.Users.Add(user);

        var credential = UserCredential.Create(user.Id, _passwordHasher.HashPassword("OldPass123!"), DateTimeOffset.UtcNow.AddDays(-5));
        _userCredentialRepo.Credentials.Add(credential);

        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);
        var resetToken = PasswordResetToken.Create(user.Id, tokenHash, DateTimeOffset.UtcNow);
        _resetTokenRepo.Tokens.Add(resetToken);

        var useCase = CreateUseCase();
        var command = new ResetPasswordCommand(rawToken, "NewPass987654#");

        // Act
        await useCase.ExecuteAsync(command);

        // Assert: El usuario permanece bloqueado y sin verificar
        Assert.Equal(UserAccountStatus.Blocked, user.Status);
        Assert.False(user.IsEmailVerified);
        Assert.Null(user.EmailVerifiedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithNullOrWhitespaceToken_ShouldThrowValidationException(string? invalidToken)
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new ResetPasswordCommand(invalidToken!, "ValidPass123#");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("obligatorio", ex.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("1234567")] // < 8
    public async Task ExecuteAsync_WithWeakPassword_ShouldThrowValidationException(string weakPassword)
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new ResetPasswordCommand("valid_token_value", weakPassword);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("8 caracteres", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentToken_ShouldThrowInvalidTokenException()
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new ResetPasswordCommand("unknown_token_value", "ValidPass123#");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("no existe o ya ha sido consumido", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredToken_ShouldThrowInvalidTokenException()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create("expired_reset@yusay.org"), DateTimeOffset.UtcNow);
        _userAccountRepo.Users.Add(user);

        var credential = UserCredential.Create(user.Id, _passwordHasher.HashPassword("OldPass123!"), DateTimeOffset.UtcNow.AddDays(-5));
        _userCredentialRepo.Credentials.Add(credential);

        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        // Token de hace 40 minutos (expiró a los 30 min)
        var creationTime = DateTimeOffset.UtcNow.AddMinutes(-40);
        var resetToken = PasswordResetToken.Create(user.Id, tokenHash, creationTime);
        _resetTokenRepo.Tokens.Add(resetToken);

        var useCase = CreateUseCase();
        var command = new ResetPasswordCommand(rawToken, "NewPass987654#");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("ha expirado", ex.Message);

        // La contraseña previa no debe haberse alterado
        Assert.True(_passwordHasher.VerifyPassword("OldPass123!", credential.PasswordHash));
    }
}
