using System.Data.Common;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Common;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Identity;

public class RegisterUserUseCaseTests
{
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakeUserCredentialRepository _userCredentialRepo = new();
    private readonly FakeEmailVerificationTokenRepository _tokenRepo = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly IPasswordHasher _passwordHasher = new Argon2idPasswordHasher(new Argon2Options { MemorySize = 1024, Iterations = 2, DegreeOfParallelism = 1 });
    private readonly ISecureTokenService _tokenService = new SecureTokenService();
    private readonly FakeEmailVerificationSender _emailSender = new();

    private RegisterUserUseCase CreateUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _tokenRepo,
        _auditEventRepo,
        _passwordHasher,
        _tokenService,
        _emailSender);

    [Fact]
    public async Task ExecuteAsync_WithValidData_ShouldRegisterUserAndIssueTokenAndAudit()
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new RegisterUserCommand(
            Email: "newuser@yusay.org",
            Password: "SecurePassword123!",
            AdultConfirmed: true);

        // Act
        var result = await useCase.ExecuteAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.Equal("newuser@yusay.org", result.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.VerificationToken));

        // Verificar UserAccount persistido
        Assert.Single(_userAccountRepo.Users);
        var createdUser = _userAccountRepo.Users.First();
        Assert.Equal(result.UserId, createdUser.Id);
        Assert.Equal("newuser@yusay.org", createdUser.Email.Value);
        Assert.Null(createdUser.EmailVerifiedAt);
        Assert.False(createdUser.IsEmailVerified);

        // Verificar UserCredential persistida
        Assert.Single(_userCredentialRepo.Credentials);
        var createdCred = _userCredentialRepo.Credentials.First();
        Assert.Equal(result.UserId, createdCred.UserId);
        Assert.True(_passwordHasher.VerifyPassword(command.Password, createdCred.PasswordHash));

        // Verificar Token persistido
        Assert.Single(_tokenRepo.Tokens);
        var createdToken = _tokenRepo.Tokens.First();
        Assert.Equal(result.UserId, createdToken.UserId);
        Assert.Equal(_tokenService.HashToken(result.VerificationToken), createdToken.TokenHash);

        // Verificar Eventos de Auditoría (USER_REGISTERED y EMAIL_VERIFICATION_TOKEN_ISSUED)
        Assert.Equal(2, _auditEventRepo.Events.Count);
        Assert.Contains(_auditEventRepo.Events, e => e.Action == "USER_REGISTERED" && e.ActorUserId == result.UserId);
        Assert.Contains(_auditEventRepo.Events, e => e.Action == "EMAIL_VERIFICATION_TOKEN_ISSUED" && e.ActorUserId == result.UserId);

        // La entrega por correo se intenta una sola vez, tras el commit, con el mismo token
        // que el caso de uso devuelve a sus llamadores (la respuesta HTTP nunca lo expone).
        var delivery = Assert.Single(_emailSender.Deliveries);
        Assert.Equal("newuser@yusay.org", delivery.Email);
        Assert.Equal(result.VerificationToken, delivery.Token);

        Assert.True(_unitOfWork.TransactionExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAdultConfirmation_ShouldThrowValidationException()
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new RegisterUserCommand(
            Email: "minor@yusay.org",
            Password: "SecurePassword123!",
            AdultConfirmed: false);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("mayoría de edad", ex.Message);
        Assert.Empty(_userAccountRepo.Users);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("short")]
    [InlineData("1234567")] // 7 caracteres < 8
    public async Task ExecuteAsync_WithWeakPassword_ShouldThrowValidationException(string weakPassword)
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new RegisterUserCommand(
            Email: "user@yusay.org",
            Password: weakPassword,
            AdultConfirmed: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("8 caracteres", ex.Message);
        Assert.Empty(_userAccountRepo.Users);
    }

    [Theory]
    [InlineData("invalid-email")]
    [InlineData("@nodomain.com")]
    [InlineData("no-at-sign")]
    public async Task ExecuteAsync_WithInvalidEmail_ShouldThrowDomainException(string invalidEmail)
    {
        // Arrange
        var useCase = CreateUseCase();
        var command = new RegisterUserCommand(
            Email: invalidEmail,
            Password: "SecurePassword123!",
            AdultConfirmed: true);

        // Act & Assert
        await Assert.ThrowsAsync<DomainException>(() => useCase.ExecuteAsync(command));
        Assert.Empty(_userAccountRepo.Users);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailAlreadyExists_ShouldThrowConflictException()
    {
        // Arrange
        var existingUser = UserAccount.Create(Email.Create("duplicate@yusay.org"), DateTimeOffset.UtcNow);
        _userAccountRepo.Users.Add(existingUser);

        var useCase = CreateUseCase();
        var command = new RegisterUserCommand(
            Email: "duplicate@yusay.org",
            Password: "SecurePassword123!",
            AdultConfirmed: true);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("Ya existe una cuenta", ex.Message);
        Assert.Single(_userAccountRepo.Users); // No se añadió un nuevo usuario
        Assert.Empty(_emailSender.Deliveries); // No se entrega ningún token si el registro falla
    }
}
