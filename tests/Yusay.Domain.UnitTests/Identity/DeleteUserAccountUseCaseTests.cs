using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Commands.DeleteUserAccount;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Identity;

public sealed class DeleteUserAccountUseCaseTests
{
    private const string ValidPassword = "NormativePassword#2026";

    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly FakeUserCredentialRepository _userCredentialRepo = new();
    private readonly FakeAuditEventRepository _auditEventRepo = new();
    private readonly FakeAccessTokenDenylist _denylist = new();
    private readonly Argon2idPasswordHasher _passwordHasher = new(new Argon2Options
    {
        MemorySize = 1024,
        Iterations = 2,
        DegreeOfParallelism = 1
    });
    private readonly JwtTokenService _jwtTokenService = new(new JwtOptions
    {
        Secret = "unit-test-secret-0123456789abcdef-0123456789abcdef",
        AccessTokenLifetimeSeconds = 900
    });

    private readonly DeleteUserAccountUseCase _sut;

    public DeleteUserAccountUseCaseTests()
    {
        _sut = new DeleteUserAccountUseCase(
            _currentUser,
            _unitOfWork,
            _userAccountRepo,
            _userCredentialRepo,
            _auditEventRepo,
            _passwordHasher,
            _jwtTokenService,
            _denylist);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrentUserIsNull_ShouldThrowUnauthorizedException()
    {
        // Arrange
        _currentUser.UserId = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword)));
        Assert.Equal("Se requiere una identidad de usuario autenticada para suprimir la cuenta.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrentUserIsEmpty_ShouldThrowUnauthorizedException()
    {
        // Arrange
        _currentUser.UserId = Guid.Empty;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword)));
        Assert.Equal("Se requiere una identidad de usuario autenticada para suprimir la cuenta.", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenPasswordIsMissingOrWhitespace_ShouldThrowValidationException(string? password)
    {
        // Arrange
        _currentUser.UserId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _sut.ExecuteAsync(new DeleteUserAccountCommand(password!)));
        Assert.Equal("La contraseña actual es obligatoria para suprimir la cuenta.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ShouldThrowUnauthorizedException()
    {
        // Arrange
        _currentUser.UserId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword)));
        Assert.Equal("Usuario no encontrado.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCredentialNotFoundOrPasswordIncorrect_ShouldThrowUnauthorizedException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var user = UserAccount.Rehydrate(
            userId,
            Email.Create("delete_wrong_pass@yusay.local"),
            emailVerifiedAt: DateTimeOffset.UtcNow,
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            adultConfirmedAt: DateTimeOffset.UtcNow.AddDays(-1),
            status: UserAccountStatus.Active);
        await _userAccountRepo.AddAsync(user);

        var credential = UserCredential.Create(userId, _passwordHasher.HashPassword(ValidPassword));
        await _userCredentialRepo.AddAsync(credential);

        _currentUser.UserId = userId;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.ExecuteAsync(new DeleteUserAccountCommand("WrongPassword#9999")));
        Assert.Equal("Credenciales no válidas.", ex.Message);

        // La cuenta no debe haber sido eliminada
        Assert.NotNull(await _userAccountRepo.GetByIdAsync(userId));
    }

    [Fact]
    public async Task ExecuteAsync_WithCorrectPassword_ShouldDeleteUserAccountAndRecordUserDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "delete_success@yusay.local";
        var user = UserAccount.Rehydrate(
            userId,
            Email.Create(email),
            emailVerifiedAt: DateTimeOffset.UtcNow,
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            adultConfirmedAt: DateTimeOffset.UtcNow.AddDays(-1),
            status: UserAccountStatus.Active);
        await _userAccountRepo.AddAsync(user);

        var credential = UserCredential.Create(userId, _passwordHasher.HashPassword(ValidPassword));
        await _userCredentialRepo.AddAsync(credential);

        var token = _jwtTokenService.IssueAccessToken(userId, email, credential.PasswordChangedAt).Token;

        _currentUser.UserId = userId;

        // Act
        await _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword, token));

        // Assert
        Assert.True(_unitOfWork.TransactionExecuted);
        Assert.Null(await _userAccountRepo.GetByIdAsync(userId));

        // Verificar emisión de USER_DELETED conforme a norma
        var audit = Assert.Single(_auditEventRepo.Events);
        Assert.Equal("USER_DELETED", audit.Action);
        Assert.Equal("USER", audit.ActorKind);
        Assert.Null(audit.ActorUserId);
        Assert.Equal("USER", audit.TargetType);
        Assert.Null(audit.TargetIdentifier);
        Assert.Null(audit.Metadata);

        // Verificar revocación en Denylist
        Assert.Equal(1, _denylist.RevokedCount);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAuditInsertFails_ShouldStillCompleteDeletionUnderDpTrans001()
    {
        // Arrange: simular fallo inyectado en repositorio de auditoría
        var userId = Guid.NewGuid();
        var email = "dp_trans_fail@yusay.local";
        var user = UserAccount.Rehydrate(
            userId,
            Email.Create(email),
            emailVerifiedAt: DateTimeOffset.UtcNow,
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            adultConfirmedAt: DateTimeOffset.UtcNow.AddDays(-1),
            status: UserAccountStatus.Active);
        await _userAccountRepo.AddAsync(user);

        var credential = UserCredential.Create(userId, _passwordHasher.HashPassword(ValidPassword));
        await _userCredentialRepo.AddAsync(credential);

        _currentUser.UserId = userId;
        _auditEventRepo.FailNextAdd = true;

        // Act: no debe propagar la excepción de auditoría
        await _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword));

        // Assert: la cuenta se eliminó igualmente según DP-TRANS-001
        Assert.Null(await _userAccountRepo.GetByIdAsync(userId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenRedisFails_ShouldNotRethrowNorCancelDeletion()
    {
        // Arrange: simular Redis caído / denylist unavailable
        var userId = Guid.NewGuid();
        var email = "redis_fail@yusay.local";
        var user = UserAccount.Rehydrate(
            userId,
            Email.Create(email),
            emailVerifiedAt: DateTimeOffset.UtcNow,
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            adultConfirmedAt: DateTimeOffset.UtcNow.AddDays(-1),
            status: UserAccountStatus.Active);
        await _userAccountRepo.AddAsync(user);

        var credential = UserCredential.Create(userId, _passwordHasher.HashPassword(ValidPassword));
        await _userCredentialRepo.AddAsync(credential);

        var token = _jwtTokenService.IssueAccessToken(userId, email, credential.PasswordChangedAt).Token;

        _currentUser.UserId = userId;
        _denylist.Unavailable = true;

        // Act: no debe lanzar excepción de Redis
        await _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword, token));

        // Assert: la cuenta en BD fue eliminada exitosamente
        Assert.Null(await _userAccountRepo.GetByIdAsync(userId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationRequestedDuringAudit_ShouldPropagateOperationCanceledException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "cancel_audit@yusay.local";
        var user = UserAccount.Rehydrate(
            userId,
            Email.Create(email),
            emailVerifiedAt: DateTimeOffset.UtcNow,
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            adultConfirmedAt: DateTimeOffset.UtcNow.AddDays(-1),
            status: UserAccountStatus.Active);
        await _userAccountRepo.AddAsync(user);

        var credential = UserCredential.Create(userId, _passwordHasher.HashPassword(ValidPassword));
        await _userCredentialRepo.AddAsync(credential);

        _currentUser.UserId = userId;
        _auditEventRepo.CustomExceptionToThrow = new OperationCanceledException();

        // Act & Assert: la cancelación NO debe ser absorbida
        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenFatalErrorOccursInAudit_ShouldPropagateException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "fatal_audit@yusay.local";
        var user = UserAccount.Rehydrate(
            userId,
            Email.Create(email),
            emailVerifiedAt: DateTimeOffset.UtcNow,
            createdAt: DateTimeOffset.UtcNow.AddDays(-1),
            adultConfirmedAt: DateTimeOffset.UtcNow.AddDays(-1),
            status: UserAccountStatus.Active);
        await _userAccountRepo.AddAsync(user);

        var credential = UserCredential.Create(userId, _passwordHasher.HashPassword(ValidPassword));
        await _userCredentialRepo.AddAsync(credential);

        _currentUser.UserId = userId;
        _auditEventRepo.CustomExceptionToThrow = new OutOfMemoryException("Fatal system error");

        // Act & Assert: errores fatales del sistema NO deben ser absorbidos
        await Assert.ThrowsAsync<OutOfMemoryException>(() =>
            _sut.ExecuteAsync(new DeleteUserAccountCommand(ValidPassword)));
    }
}
