using Npgsql;
using Xunit;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

[Collection("DatabaseCollection")]
public sealed class UserCredentialRepositoryTests
{
    private readonly UserAccountRepository _userRepository;
    private readonly UserCredentialRepository _credentialRepository;

    public UserCredentialRepositoryTests(PostgreSqlFixture fixture)
    {
        _userRepository = new UserAccountRepository(fixture.ConnectionFactory);
        _credentialRepository = new UserCredentialRepository(fixture.ConnectionFactory);
    }

    [Fact]
    public async Task AddAsync_And_GetByUserIdAsync_ShouldPersistAndRehydrateCorrectly()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"cred_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user);

        const string hash = "$argon2id$v=19$m=65536,t=3,p=4$dummyhash1";
        var credential = UserCredential.Create(user.Id, hash);

        // Act
        await _credentialRepository.AddAsync(credential);
        var retrieved = await _credentialRepository.GetByUserIdAsync(user.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(user.Id, retrieved.UserId);
        Assert.Equal(hash, retrieved.PasswordHash);
        Assert.True(Math.Abs((credential.PasswordChangedAt - retrieved.PasswordChangedAt).TotalMilliseconds) < 1);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateHashAndTimestamp()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"upd_cred_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user);

        var initialTime = DateTimeOffset.UtcNow.AddHours(-2);
        var credential = UserCredential.Create(user.Id, "old_hash", initialTime);
        await _credentialRepository.AddAsync(credential);

        var newTime = DateTimeOffset.UtcNow;
        const string newHash = "$argon2id$v=19$m=65536,t=3,p=4$newhash2";
        credential.ChangePassword(newHash, newTime);

        // Act
        await _credentialRepository.UpdateAsync(credential);
        var retrieved = await _credentialRepository.GetByUserIdAsync(user.Id);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(newHash, retrieved.PasswordHash);
        Assert.True(Math.Abs((newTime - retrieved.PasswordChangedAt).TotalMilliseconds) < 1);
    }

    [Fact]
    public async Task AddAsync_WithNonExistentUserId_ShouldThrowPostgresForeignKeyViolation()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var credential = UserCredential.Create(nonExistentUserId, "dummy_hash");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _credentialRepository.AddAsync(credential));
        Assert.Equal("23503", ex.SqlState); // foreign_key_violation (fk_user_credential_user_account)
    }

    [Fact]
    public async Task CascadeDelete_WhenUserAccountDeleted_ShouldCascadeDeleteCredential()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"casc_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user);

        var credential = UserCredential.Create(user.Id, "cascade_hash");
        await _credentialRepository.AddAsync(credential);

        // Act: Eliminar la cuenta del usuario
        await _userRepository.DeleteAsync(user.Id);

        // Assert: La credencial debe haber sido eliminada por ON DELETE CASCADE
        var retrievedCred = await _credentialRepository.GetByUserIdAsync(user.Id);
        Assert.Null(retrievedCred);
    }
}
