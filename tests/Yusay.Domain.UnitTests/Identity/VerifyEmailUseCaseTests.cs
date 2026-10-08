using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Identity;

public class VerifyEmailUseCaseTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakeEmailVerificationTokenRepository _tokenRepo = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly SecureTokenService _tokenService = new();

    private VerifyEmailUseCase CreateUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _tokenRepo,
        _auditEventRepo,
        _tokenService);

    [Fact]
    public async Task ExecuteAsync_WithValidToken_ShouldVerifyEmailAndInvalidateTokensAndAudit()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create("verify@yusay.org"), DateTimeOffset.UtcNow);
        _userAccountRepo.Users.Add(user);

        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);
        var token = EmailVerificationToken.Create(user.Id, tokenHash, DateTimeOffset.UtcNow);
        _tokenRepo.Tokens.Add(token);

        var useCase = CreateUseCase();
        var command = new VerifyEmailCommand(rawToken);

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("verify@yusay.org", result.Email);

        // Usuario debe estar verificado
        Assert.True(user.IsEmailVerified);
        Assert.NotNull(user.EmailVerifiedAt);

        // Token debe haber sido eliminado (consumo único)
        Assert.Empty(_tokenRepo.Tokens);

        // Auditoría EMAIL_VERIFIED registrada
        Assert.Single(_auditEventRepo.Events);
        var audit = _auditEventRepo.Events.First();
        Assert.Equal("EMAIL_VERIFIED", audit.Action);
        Assert.Equal(user.Id, audit.ActorUserId);

        Assert.True(_unitOfWork.TransactionExecuted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithNullOrWhitespaceToken_ShouldThrowValidationException(string? invalidToken)
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new VerifyEmailCommand(invalidToken!);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("obligatorio", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithNonExistentToken_ShouldThrowInvalidTokenException()
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new VerifyEmailCommand("non_existent_token_value");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("no existe o ya ha sido consumido", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithExpiredToken_ShouldRejectAndThrowInvalidTokenException()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create("expired@yusay.org"), DateTimeOffset.UtcNow.AddDays(-2));
        _userAccountRepo.Users.Add(user);

        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);

        // Token creado hace 26 horas (expira a las 24 horas)
        var creationTime = DateTimeOffset.UtcNow.AddHours(-26);
        var token = EmailVerificationToken.Create(user.Id, tokenHash, creationTime);
        _tokenRepo.Tokens.Add(token);

        var useCase = CreateUseCase();
        var command = new VerifyEmailCommand(rawToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<InvalidTokenException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("ha expirado", ex.Message);

        // Usuario no fue verificado
        Assert.False(user.IsEmailVerified);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ShouldThrowNotFoundException()
    {
        // Arrange: Token apunta a un usuario que no existe
        var rawToken = _tokenService.GenerateToken();
        var tokenHash = _tokenService.HashToken(rawToken);
        var orphanedToken = EmailVerificationToken.Create(Guid.NewGuid(), tokenHash, DateTimeOffset.UtcNow);
        _tokenRepo.Tokens.Add(orphanedToken);

        var useCase = CreateUseCase();
        var command = new VerifyEmailCommand(rawToken);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("No se encontró la cuenta", ex.Message);
    }
}
