using Yusay.Domain.Common;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Domain.UnitTests.Identity;

public sealed class EmailTests
{
    [Theory]
    [InlineData("user@example.com", "user@example.com")]
    [InlineData("USER@EXAMPLE.COM", "user@example.com")]
    [InlineData("  user.name+tag@sub.domain.org  ", "user.name+tag@sub.domain.org")]
    [InlineData("test123_abc@domain-test.co", "test123_abc@domain-test.co")]
    public void Create_WithValidEmail_ShouldNormalizeToLowerCaseAndTrim(string input, string expected)
    {
        // Act
        var email = Email.Create(input);

        // Assert
        Assert.Equal(expected, email.Value);
        Assert.Equal(expected, (string)email);
    }

    [Fact]
    public void Equals_WithDifferentCasing_ShouldBeEqual()
    {
        // Arrange & Act
        var email1 = Email.Create("Alpha@Test.com");
        var email2 = Email.Create("alpha@test.com");

        // Assert
        Assert.Equal(email1, email2);
        Assert.True(email1 == email2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    [InlineData("username@")]
    [InlineData("user@domain@domain.com")]
    [InlineData(".leadingdot@domain.com")]
    [InlineData("trailingdot.@domain.com")]
    [InlineData("user..double@domain.com")]
    [InlineData("user@domain..com")]
    public void Create_WithInvalidEmail_ShouldThrowDomainException(string? invalidInput)
    {
        // Act & Assert
        Assert.Throws<DomainException>(() => Email.Create(invalidInput!));
    }

    [Fact]
    public void Create_WithEmailExceedingMaxLength_ShouldThrowDomainException()
    {
        // Arrange: 255 characters
        string longLocal = new string('a', 245);
        string longEmail = $"{longLocal}@domain.com";

        // Act & Assert
        Assert.True(longEmail.Length > Email.MaxLength);
        Assert.Throws<DomainException>(() => Email.Create(longEmail));
    }
}
