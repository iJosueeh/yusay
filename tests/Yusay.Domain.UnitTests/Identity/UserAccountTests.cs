using Yusay.Domain.Common;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Domain.UnitTests.Identity;

public sealed class UserAccountTests
{
    private readonly Email _validEmail = Email.Create("user@yusay.local");
    private readonly DateTimeOffset _validAdultInstant = DateTimeOffset.UtcNow.AddYears(-20);

    [Fact]
    public void Create_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Act
        var user = UserAccount.Create(_validEmail, _validAdultInstant);

        // Assert
        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal(_validEmail, user.Email);
        Assert.Equal(UserAccountStatus.Active, user.Status);
        Assert.Null(user.EmailVerifiedAt);
        Assert.False(user.IsEmailVerified);
        Assert.False(user.CanAccessPersonalFeatures);
        Assert.Equal(_validAdultInstant, user.AdultConfirmedAt);
    }

    [Fact]
    public void Create_WithoutAdultConfirmation_ShouldThrowDomainException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => UserAccount.Create(_validEmail, default));
        Assert.Throws<DomainException>(() => UserAccount.Create(_validEmail, DateTimeOffset.MinValue));
    }

    [Fact]
    public void Create_WithEmptyGuid_ShouldThrowDomainException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => UserAccount.Create(_validEmail, _validAdultInstant, id: Guid.Empty));
    }

    [Fact]
    public void VerifyEmail_WithValidTimestamp_ShouldEnablePersonalFeatures()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.Create(_validEmail, _validAdultInstant, createdAt: now);

        // Act
        user.VerifyEmail(now.AddMinutes(5));

        // Assert
        Assert.True(user.IsEmailVerified);
        Assert.Equal(now.AddMinutes(5), user.EmailVerifiedAt);
        Assert.True(user.CanAccessPersonalFeatures);
    }

    [Fact]
    public void VerifyEmail_WithTimestampPriorToCreation_ShouldThrowDomainException()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.Create(_validEmail, _validAdultInstant, createdAt: now);

        // Act & Assert
        Assert.Throws<DomainException>(() => user.VerifyEmail(now.AddMinutes(-10)));
    }

    [Fact]
    public void Block_ShouldPreventPersonalFeatureAccess()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.Create(_validEmail, _validAdultInstant, createdAt: now);
        user.VerifyEmail(now.AddMinutes(1));
        Assert.True(user.CanAccessPersonalFeatures);

        // Act
        user.Block();

        // Assert
        Assert.Equal(UserAccountStatus.Blocked, user.Status);
        Assert.False(user.CanAccessPersonalFeatures);
    }

    [Fact]
    public void Unblock_ShouldRestorePersonalFeatureAccessIfVerified()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.Create(_validEmail, _validAdultInstant, createdAt: now);
        user.VerifyEmail(now.AddMinutes(1));
        user.Block();
        Assert.False(user.CanAccessPersonalFeatures);

        // Act
        user.Unblock();

        // Assert
        Assert.Equal(UserAccountStatus.Active, user.Status);
        Assert.True(user.CanAccessPersonalFeatures);
    }
}
