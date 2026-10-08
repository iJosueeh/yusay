using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.RequestPasswordReset;
using Yusay.Domain.Common;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Identity;

public class RequestPasswordResetUseCaseTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakePasswordResetTokenRepository _resetTokenRepo = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly SecureTokenService _tokenService = new();

    private RequestPasswordResetUseCase CreateUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _resetTokenRepo,
        _auditEventRepo,
        _tokenService);

    [Fact]
    public async Task ExecuteAsync_WhenUserExists_ShouldIssue30MinTokenAndInvalidatePriorTokensAndAudit()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create("reset@yusay.org"), DateTimeOffset.UtcNow);
        _userAccountRepo.Users.Add(user);

        // Token previo que debe ser invalidado
        var oldToken = PasswordResetToken.Create(user.Id, "old_hash", DateTimeOffset.UtcNow.AddMinutes(-10));
        _resetTokenRepo.Tokens.Add(oldToken);

        var useCase = CreateUseCase();
        var command = new RequestPasswordResetCommand("reset@yusay.org");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.EmailSent);
        Assert.NotNull(result.ResetToken);
        Assert.False(string.IsNullOrWhiteSpace(result.ResetToken));

        // Debe haber exactamente 1 token en el repositorio (el nuevo) y el previo debe haber sido invalidado
        Assert.Single(_resetTokenRepo.Tokens);
        var createdToken = _resetTokenRepo.Tokens.First();
        Assert.Equal(user.Id, createdToken.UserId);
        Assert.Equal(_tokenService.HashToken(result.ResetToken), createdToken.TokenHash);
        
        // Vigencia normativa de exactamente 30 minutos
        var duration = createdToken.ExpiresAt - createdToken.CreatedAt;
        Assert.Equal(TimeSpan.FromMinutes(30), duration);

        // Auditoría PASSWORD_RESET_TOKEN_ISSUED
        Assert.Single(_auditEventRepo.Events);
        var audit = _auditEventRepo.Events.First();
        Assert.Equal("PASSWORD_RESET_TOKEN_ISSUED", audit.Action);
        Assert.Equal(user.Id, audit.ActorUserId);

        Assert.True(_unitOfWork.TransactionExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserDoesNotExist_ShouldReturnGenericSuccessWithoutRevealingExistence()
    {
        // Arrange: El usuario NO existe en el repositorio
        var useCase = CreateUseCase();
        var command = new RequestPasswordResetCommand("unknown_user@yusay.org");

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert: Respuesta indistinguible (no arroja excepción, EmailSent es true, pero no genera token interno)
        Assert.NotNull(result);
        Assert.True(result.EmailSent);
        Assert.Null(result.ResetToken);

        // No se debe persistir ningún token ni evento de auditoría para usuario inexistente
        Assert.Empty(_resetTokenRepo.Tokens);
        Assert.Empty(_auditEventRepo.Events);
        Assert.True(_unitOfWork.TransactionExecuted);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_WithNullOrWhitespaceEmail_ShouldThrowValidationException(string? invalidEmail)
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new RequestPasswordResetCommand(invalidEmail!);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("obligatorio", ex.Message);
        Assert.Empty(_resetTokenRepo.Tokens);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@missingusername.com")]
    public async Task ExecuteAsync_WithMalformedEmail_ShouldThrowDomainException(string malformedEmail)
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new RequestPasswordResetCommand(malformedEmail);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(command));
        Assert.Empty(_resetTokenRepo.Tokens);
    }
}
