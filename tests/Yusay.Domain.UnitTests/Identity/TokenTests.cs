using Yusay.Domain.Common;
using Yusay.Domain.Identity.Entities;

namespace Yusay.Domain.UnitTests.Identity;

public sealed class TokenTests
{
    private readonly Guid _validUserId = Guid.NewGuid();
    private const string ValidHash = "a1b2c3d4e5f67890abcdef1234567890abcdef1234567890abcdef1234567890";

    [Fact]
    public void EmailVerificationToken_Create_ShouldHaveExact24HoursValidity()
    {
        // Arrange
        var created = DateTimeOffset.UtcNow;

        // Act
        var token = EmailVerificationToken.Create(_validUserId, ValidHash, created);

        // Assert
        Assert.NotEqual(Guid.Empty, token.VerificationTokenId);
        Assert.Equal(_validUserId, token.UserId);
        Assert.Equal(ValidHash, token.TokenHash);
        Assert.Equal(created, token.CreatedAt);
        Assert.Equal(created.AddHours(24), token.ExpiresAt);
        Assert.Equal(TimeSpan.FromHours(24), EmailVerificationToken.ValidityDuration);

        // Temporal predicates
        Assert.True(token.IsValid(created.AddHours(23)));
        Assert.False(token.IsExpired(created.AddHours(23)));

        Assert.False(token.IsValid(created.AddHours(24)));
        Assert.True(token.IsExpired(created.AddHours(24)));

        Assert.False(token.IsValid(created.AddHours(25)));
        Assert.True(token.IsExpired(created.AddHours(25)));
    }

    [Fact]
    public void PasswordResetToken_Create_ShouldHaveExact30MinutesValidity()
    {
        // Arrange
        var created = DateTimeOffset.UtcNow;

        // Act
        var token = PasswordResetToken.Create(_validUserId, ValidHash, created);

        // Assert
        Assert.NotEqual(Guid.Empty, token.ResetTokenId);
        Assert.Equal(_validUserId, token.UserId);
        Assert.Equal(ValidHash, token.TokenHash);
        Assert.Equal(created, token.CreatedAt);
        Assert.Equal(created.AddMinutes(30), token.ExpiresAt);
        Assert.Equal(TimeSpan.FromMinutes(30), PasswordResetToken.ValidityDuration);

        // Temporal predicates
        Assert.True(token.IsValid(created.AddMinutes(29)));
        Assert.False(token.IsExpired(created.AddMinutes(29)));

        Assert.False(token.IsValid(created.AddMinutes(30)));
        Assert.True(token.IsExpired(created.AddMinutes(30)));

        Assert.False(token.IsValid(created.AddMinutes(31)));
        Assert.True(token.IsExpired(created.AddMinutes(31)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Token_Create_WithInvalidHash_ShouldThrowDomainException(string? invalidHash)
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<DomainException>(() => EmailVerificationToken.Create(_validUserId, invalidHash!, now));
        Assert.Throws<DomainException>(() => PasswordResetToken.Create(_validUserId, invalidHash!, now));
    }

    [Fact]
    public void Token_Create_WithEmptyUserId_ShouldThrowDomainException()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<DomainException>(() => EmailVerificationToken.Create(Guid.Empty, ValidHash, now));
        Assert.Throws<DomainException>(() => PasswordResetToken.Create(Guid.Empty, ValidHash, now));
    }
}
