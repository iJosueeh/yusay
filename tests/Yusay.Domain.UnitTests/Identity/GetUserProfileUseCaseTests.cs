using Yusay.Application.Common.Exceptions;
using Yusay.Application.Identity.Queries.GetUserProfile;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Identity;

public sealed class GetUserProfileUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUserAccountRepository _userAccountRepo = new();
    private readonly GetUserProfileUseCase _sut;

    public GetUserProfileUseCaseTests()
    {
        _sut = new GetUserProfileUseCase(_currentUser, _userAccountRepo);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrentUserIsNull_ShouldThrowUnauthorizedException()
    {
        // Arrange
        _currentUser.UserId = null;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.ExecuteAsync(new GetUserProfileQuery()));
        Assert.Equal("Se requiere una identidad de usuario autenticada para acceder al perfil.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCurrentUserIsEmptyGuid_ShouldThrowUnauthorizedException()
    {
        // Arrange
        _currentUser.UserId = Guid.Empty;

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.ExecuteAsync(new GetUserProfileQuery()));
        Assert.Equal("Se requiere una identidad de usuario autenticada para acceder al perfil.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFoundInRepository_ShouldThrowUnauthorizedException()
    {
        // Arrange
        _currentUser.UserId = Guid.NewGuid();

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _sut.ExecuteAsync(new GetUserProfileQuery()));
        Assert.Equal("Usuario no encontrado.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserExistsAndIsVerified_ShouldReturnCorrectProfileDto()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "profile.user@yusay.local";
        var createdAt = DateTimeOffset.UtcNow.AddDays(-10);
        var adultConfirmedAt = createdAt.AddMinutes(-5);
        var verifiedAt = createdAt.AddHours(1);

        var user = UserAccount.Rehydrate(
            userId,
            Email.Create(email),
            emailVerifiedAt: verifiedAt,
            createdAt: createdAt,
            adultConfirmedAt: adultConfirmedAt,
            status: UserAccountStatus.Active);

        await _userAccountRepo.AddAsync(user);
        _currentUser.UserId = userId;

        // Act
        var result = await _sut.ExecuteAsync(new GetUserProfileQuery());

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(email, result.Email);
        Assert.True(result.EmailVerified);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(adultConfirmedAt, result.AdultConfirmedAt);
        Assert.Equal("ACTIVE", result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserEmailNotVerified_ShouldReturnEmailVerifiedFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "unverified.user@yusay.local";
        var createdAt = DateTimeOffset.UtcNow.AddDays(-2);
        var adultConfirmedAt = createdAt.AddMinutes(-1);

        var user = UserAccount.Rehydrate(
            userId,
            Email.Create(email),
            emailVerifiedAt: null,
            createdAt: createdAt,
            adultConfirmedAt: adultConfirmedAt,
            status: UserAccountStatus.Active);

        await _userAccountRepo.AddAsync(user);
        _currentUser.UserId = userId;

        // Act
        var result = await _sut.ExecuteAsync(new GetUserProfileQuery());

        // Assert
        Assert.False(result.EmailVerified);
        Assert.Equal("ACTIVE", result.Status);
    }

    [Fact]
    public void UserProfileDto_ShouldNotContainSensitiveFields()
    {
        // Verifica que la definición de UserProfileDto no exponga credenciales, contraseñas, roles ni salud
        var propertyNames = typeof(Yusay.Application.Identity.Dtos.UserProfileDto)
            .GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        var forbiddenSubstrings = new[] { "password", "hash", "secret", "token", "role", "health", "score" };
        foreach (var prop in propertyNames)
        {
            foreach (var forbidden in forbiddenSubstrings)
            {
                Assert.DoesNotContain(forbidden, prop);
            }
        }
    }
}
