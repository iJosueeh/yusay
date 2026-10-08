using Npgsql;
using Xunit;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.Enums;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

[Collection("DatabaseCollection")]
public sealed class UserAccountRepositoryTests
{
    private readonly UserAccountRepository _repository;

    public UserAccountRepositoryTests(PostgreSqlFixture fixture)
    {
        _repository = new UserAccountRepository(fixture.ConnectionFactory);
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync_ShouldPersistAndRehydrateCorrectly()
    {
        // Arrange
        var email = Email.Create($"test_{Guid.NewGuid():N}@yusay.local");
        var adultConfirmedAt = DateTimeOffset.UtcNow.AddYears(-22);
        var user = UserAccount.Create(email, adultConfirmedAt);

        // Act
        await _repository.AddAsync(user);
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.Id);
        Assert.Equal(user.Email, retrieved.Email);
        Assert.Equal(UserAccountStatus.Active, retrieved.Status);
        Assert.Null(retrieved.EmailVerifiedAt);
        Assert.False(retrieved.IsEmailVerified);
        Assert.False(retrieved.CanAccessPersonalFeatures);
    }

    [Fact]
    public async Task GetByEmailAsync_ShouldMatchCaseInsensitively()
    {
        // Arrange
        string unique = Guid.NewGuid().ToString("N");
        var email = Email.Create($"Case_{unique}@Yusay.Local");
        var user = UserAccount.Create(email, DateTimeOffset.UtcNow.AddYears(-20));
        await _repository.AddAsync(user);

        // Act - Query with uppercase variations
        var queryEmail = Email.Create($"CASE_{unique}@YUSAY.LOCAL");
        var found = await _repository.GetByEmailAsync(queryEmail);
        var exists = await _repository.ExistsByEmailAsync(queryEmail);

        // Assert
        Assert.NotNull(found);
        Assert.Equal(user.Id, found.Id);
        Assert.True(exists);
    }

    [Fact]
    public async Task AddAsync_WithDuplicateEmail_ShouldThrowPostgresUniqueViolation()
    {
        // Arrange
        string unique = Guid.NewGuid().ToString("N");
        var email1 = Email.Create($"dup_{unique}@yusay.local");
        var user1 = UserAccount.Create(email1, DateTimeOffset.UtcNow.AddYears(-25));
        await _repository.AddAsync(user1);

        // Act & Assert: Intento de insertar otro usuario con el mismo correo (en diferente casing)
        var email2 = Email.Create($"DUP_{unique}@YUSAY.LOCAL");
        var user2 = UserAccount.Create(email2, DateTimeOffset.UtcNow.AddYears(-25));

        var ex = await Assert.ThrowsAsync<PostgresException>(() => _repository.AddAsync(user2));
        Assert.Equal("23505", ex.SqlState); // unique_violation (uq_user_account_email)
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistVerificationAndStatusChanges()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"update_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _repository.AddAsync(user);

        var verifyInstant = DateTimeOffset.UtcNow;
        user.VerifyEmail(verifyInstant);
        user.Block();

        // Act
        await _repository.UpdateAsync(user);
        var updated = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.NotNull(updated);
        Assert.Equal(UserAccountStatus.Blocked, updated.Status);
        Assert.NotNull(updated.EmailVerifiedAt);
        Assert.False(updated.CanAccessPersonalFeatures); // Bloqueado, por lo que no puede acceder aunque esté verificado
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveUserAccount()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"del_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _repository.AddAsync(user);

        // Act
        await _repository.DeleteAsync(user.Id);
        var retrieved = await _repository.GetByIdAsync(user.Id);

        // Assert
        Assert.Null(retrieved);
    }
}
