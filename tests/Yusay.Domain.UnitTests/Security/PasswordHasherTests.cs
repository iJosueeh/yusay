using Yusay.Infrastructure.Identity.Services;

namespace Yusay.Domain.UnitTests.Security;

public class PasswordHasherTests
{
    // Usamos parámetros reducidos para agilizar la ejecución del test suite unitario
    private readonly Argon2Options _fastOptions = new()
    {
        MemorySize = 1024, // 1 MiB
        Iterations = 2,
        DegreeOfParallelism = 1,
        SaltSize = 16,
        HashSize = 32
    };

    [Fact]
    public void HashPassword_ShouldGenerateStandardModularCryptFormat()
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher(_fastOptions);
        var password = "SuperSecretPassword#2026";

        // Act
        var hash = hasher.HashPassword(password);

        // Assert
        Assert.NotNull(hash);
        Assert.StartsWith("$argon2id$v=19$m=", hash);
        var parts = hash.Split('$');
        Assert.Equal(6, parts.Length);
        Assert.Equal("argon2id", parts[1]);
        Assert.Equal("v=19", parts[2]);
        Assert.Contains("m=1024,t=2,p=1", parts[3]);
        Assert.False(string.IsNullOrWhiteSpace(parts[4])); // Salt Base64
        Assert.False(string.IsNullOrWhiteSpace(parts[5])); // Hash Base64
    }

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ShouldReturnTrue()
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher(_fastOptions);
        var password = "P@ssw0rdValidNormative!";
        var hash = hasher.HashPassword(password);

        // Act
        var result = hasher.VerifyPassword(password, hash);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ShouldReturnFalse()
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher(_fastOptions);
        var password = "CorrectPassword123";
        var hash = hasher.HashPassword(password);

        // Act
        var result = hasher.VerifyPassword("WrongPassword456", hash);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void HashPassword_WithSamePasswordMultipleTimes_ShouldGenerateDifferentHashesDueToSalt()
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher(_fastOptions);
        var password = "IdenticalPasswordToTestSalt";

        // Act
        var hash1 = hasher.HashPassword(password);
        var hash2 = hasher.HashPassword(password);

        // Assert
        Assert.NotEqual(hash1, hash2);
        Assert.True(hasher.VerifyPassword(password, hash1));
        Assert.True(hasher.VerifyPassword(password, hash2));
    }

    [Fact]
    public void VerifyPassword_ShouldRespectSerializedHashParameters_EvenIfHasherDefaultDiffers()
    {
        // Arrange: Hasher A genera con opciones A
        var optionsA = new Argon2Options { MemorySize = 1024, Iterations = 2, DegreeOfParallelism = 1 };
        var hasherA = new Argon2idPasswordHasher(optionsA);
        var password = "MultiParameterPassword!";
        var hashFromA = hasherA.HashPassword(password);

        // Act: Hasher B tiene opciones distintas pero debe verificar correctamente usando los metadatos serializados
        var optionsB = new Argon2Options { MemorySize = 2048, Iterations = 3, DegreeOfParallelism = 2 };
        var hasherB = new Argon2idPasswordHasher(optionsB);
        var result = hasherB.VerifyPassword(password, hashFromA);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HashPassword_WithNullOrWhitespace_ShouldThrowArgumentException(string? invalidPassword)
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher(_fastOptions);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => hasher.HashPassword(invalidPassword!));
    }

    [Theory]
    [InlineData(null, "hash")]
    [InlineData("", "hash")]
    [InlineData("password", null)]
    [InlineData("password", "")]
    [InlineData("password", "invalid_format_string")]
    [InlineData("password", "$argon2id$v=18$m=1024,t=2,p=1$c2FsdA==$aGFzaA==")] // Versión distinta
    [InlineData("password", "$bcrypt$v=19$m=1024,t=2,p=1$c2FsdA==$aGFzaA==")] // Algoritmo distinto
    [InlineData("password", "$argon2id$v=19$not_valid_params$c2FsdA==$aGFzaA==")] // Parámetros malformados
    public void VerifyPassword_WithInvalidInputs_ShouldReturnFalse(string? password, string? hash)
    {
        // Arrange
        var hasher = new Argon2idPasswordHasher(_fastOptions);

        // Act
        var result = hasher.VerifyPassword(password!, hash!);

        // Assert
        Assert.False(result);
    }
}
