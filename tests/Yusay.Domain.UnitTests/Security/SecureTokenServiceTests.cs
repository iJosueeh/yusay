using System.Text.RegularExpressions;
using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Security;

public class SecureTokenServiceTests
{
    private readonly SecureTokenService _tokenService = new();

    [Fact]
    public void GenerateToken_DefaultParameters_ShouldGenerateNonEmptyUrlSafeString()
    {
        // Act
        var token = _tokenService.GenerateToken();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(token));
        // Base64Url para 32 bytes produce exactamente 43 caracteres sin +, / o =
        Assert.Equal(43, token.Length);
        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void GenerateToken_CalledMultipleTimes_ShouldProduceUniqueTokens()
    {
        // Arrange
        const int iterations = 100;
        var tokens = new HashSet<string>();

        // Act
        for (int i = 0; i < iterations; i++)
        {
            var token = _tokenService.GenerateToken();
            tokens.Add(token);
        }

        // Assert: Todos los tokens deben ser mutuamente únicos (alta entropía CSPRNG)
        Assert.Equal(iterations, tokens.Count);
    }

    [Theory]
    [InlineData(16, 22)] // 16 bytes = 22 caracteres base64url
    [InlineData(32, 43)] // 32 bytes = 43 caracteres base64url
    [InlineData(64, 86)] // 64 bytes = 86 caracteres base64url
    public void GenerateToken_WithCustomByteLength_ShouldProduceExpectedLength(int byteLength, int expectedCharLength)
    {
        // Act
        var token = _tokenService.GenerateToken(byteLength);

        // Assert
        Assert.Equal(expectedCharLength, token.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-32)]
    public void GenerateToken_WithInvalidByteLength_ShouldThrowArgumentOutOfRangeException(int invalidLength)
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => _tokenService.GenerateToken(invalidLength));
    }

    [Fact]
    public void HashToken_ShouldGenerateDeterministic64CharLowercaseHexSha256()
    {
        // Arrange
        var token = "YusaySecureVerificationTokenSampleValue123456";

        // Act
        var hash1 = _tokenService.HashToken(token);
        var hash2 = _tokenService.HashToken(token);

        // Assert
        Assert.Equal(hash1, hash2); // Determinista para indexación y búsqueda
        Assert.Equal(64, hash1.Length);
        Assert.Matches("^[0-9a-f]{64}$", hash1); // Exactamente 64 caracteres hexadecimales en minúsculas
    }

    [Fact]
    public void HashToken_WithKnownInput_ShouldMatchKnownSha256Vector()
    {
        // Arrange: Vector de prueba conocido SHA-256("abc") = ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad
        var input = "abc";
        var expectedHash = "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad";

        // Act
        var computedHash = _tokenService.HashToken(input);

        // Assert
        Assert.Equal(expectedHash, computedHash);
    }

    [Fact]
    public void HashToken_DifferentTokens_ShouldProduceDifferentHashes()
    {
        // Arrange
        var token1 = _tokenService.GenerateToken();
        var token2 = _tokenService.GenerateToken();

        // Act
        var hash1 = _tokenService.HashToken(token1);
        var hash2 = _tokenService.HashToken(token2);

        // Assert
        Assert.NotEqual(hash1, hash2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashToken_WithNullOrWhitespace_ShouldThrowArgumentException(string? invalidToken)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _tokenService.HashToken(invalidToken!));
    }
}
