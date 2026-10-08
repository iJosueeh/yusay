using Yusay.Domain.Common;
using Yusay.Domain.Identity.Entities;

namespace Yusay.Domain.UnitTests.Identity;

public sealed class UserCredentialTests
{
    private readonly Guid _validUserId = Guid.NewGuid();
    private const string ValidHash = "$argon2id$v=19$m=65536,t=3,p=4$dummyhashvalue";

    [Fact]
    public void Create_WithValidParameters_ShouldInitializeCorrectly()
    {
        // Act
        var credential = UserCredential.Create(_validUserId, ValidHash);

        // Assert
        Assert.Equal(_validUserId, credential.UserId);
        Assert.Equal(ValidHash, credential.PasswordHash);
        Assert.True(credential.PasswordChangedAt <= DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidHash_ShouldThrowDomainException(string? invalidHash)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => UserCredential.Create(_validUserId, invalidHash!));
    }

    [Fact]
    public void Create_WithEmptyUserId_ShouldThrowDomainException()
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => UserCredential.Create(Guid.Empty, ValidHash));
    }

    [Fact]
    public void ChangePassword_WithValidNewHash_ShouldUpdateHashAndTimestamp()
    {
        // Arrange
        var initialTime = DateTimeOffset.UtcNow.AddHours(-1);
        var credential = UserCredential.Create(_validUserId, ValidHash, initialTime);
        const string newHash = "$argon2id$v=19$m=65536,t=3,p=4$newhashvalue";
        var changeTime = DateTimeOffset.UtcNow;

        // Act
        credential.ChangePassword(newHash, changeTime);

        // Assert
        Assert.Equal(newHash, credential.PasswordHash);
        Assert.Equal(changeTime, credential.PasswordChangedAt);
    }

    [Fact]
    public void ChangePassword_WithTimePriorToCurrent_ShouldThrowDomainException()
    {
        // Arrange
        var initialTime = DateTimeOffset.UtcNow;
        var credential = UserCredential.Create(_validUserId, ValidHash, initialTime);
        const string newHash = "$argon2id$v=19$m=65536,t=3,p=4$newhashvalue";

        // Act & Assert
        Assert.Throws<DomainException>(() => credential.ChangePassword(newHash, initialTime.AddMinutes(-5)));
    }
}
