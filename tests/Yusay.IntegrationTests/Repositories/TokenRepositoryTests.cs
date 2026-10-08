using Npgsql;
using Xunit;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

[Collection("DatabaseCollection")]
public sealed class TokenRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly UserAccountRepository _userRepository;
    private readonly UserCredentialRepository _credentialRepository;
    private readonly EmailVerificationTokenRepository _verificationRepo;
    private readonly PasswordResetTokenRepository _resetRepo;

    public TokenRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _userRepository = new UserAccountRepository(fixture.ConnectionFactory);
        _credentialRepository = new UserCredentialRepository(fixture.ConnectionFactory);
        _verificationRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
        _resetRepo = new PasswordResetTokenRepository(fixture.ConnectionFactory);
    }

    [Fact]
    public async Task EmailVerificationToken_AddAndGetByHash_ShouldPersistAndRehydrate()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"tok_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user);

        string hash = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var token = EmailVerificationToken.Create(user.Id, hash, now);

        // Act
        await _verificationRepo.AddAsync(token);
        var retrieved = await _verificationRepo.GetByHashAsync(hash);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(token.VerificationTokenId, retrieved.VerificationTokenId);
        Assert.Equal(user.Id, retrieved.UserId);
        Assert.Equal(hash, retrieved.TokenHash);
        Assert.True(Math.Abs((token.ExpiresAt - retrieved.ExpiresAt).TotalMilliseconds) < 1);
        Assert.True(retrieved.IsValid(now.AddHours(1)));
    }

    [Fact]
    public async Task EmailVerificationToken_DuplicateHash_ShouldThrowUniqueViolation()
    {
        // Arrange
        var user1 = UserAccount.Create(Email.Create($"u1_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        var user2 = UserAccount.Create(Email.Create($"u2_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user1);
        await _userRepository.AddAsync(user2);

        string sharedHash = "shared_unique_hash_" + Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var token1 = EmailVerificationToken.Create(user1.Id, sharedHash, now);
        var token2 = EmailVerificationToken.Create(user2.Id, sharedHash, now);

        await _verificationRepo.AddAsync(token1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _verificationRepo.AddAsync(token2));
        Assert.Equal("23505", ex.SqlState); // unique_violation (uq_email_verification_token_hash)
    }

    [Fact]
    public async Task InvalidateAllForUserAsync_ShouldRemovePreviousTokens()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"inval_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user);

        var now = DateTimeOffset.UtcNow;
        var t1 = EmailVerificationToken.Create(user.Id, $"hash_1_{Guid.NewGuid():N}", now);
        var t2 = EmailVerificationToken.Create(user.Id, $"hash_2_{Guid.NewGuid():N}", now.AddMinutes(1));
        await _verificationRepo.AddAsync(t1);
        await _verificationRepo.AddAsync(t2);

        // Act: Invalidar todos los tokens anteriores para emitir uno nuevo
        await _verificationRepo.InvalidateAllForUserAsync(user.Id);

        // Assert
        Assert.Null(await _verificationRepo.GetByHashAsync(t1.TokenHash));
        Assert.Null(await _verificationRepo.GetByHashAsync(t2.TokenHash));
    }

    [Fact]
    public async Task PasswordResetToken_AddAndGetByHash_ShouldPersistAndRehydrate()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"reset_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user);

        string hash = Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var token = PasswordResetToken.Create(user.Id, hash, now);

        // Act
        await _resetRepo.AddAsync(token);
        var retrieved = await _resetRepo.GetByHashAsync(hash);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(token.ResetTokenId, retrieved.ResetTokenId);
        Assert.Equal(user.Id, retrieved.UserId);
        Assert.Equal(hash, retrieved.TokenHash);
        Assert.True(Math.Abs((now.AddMinutes(30) - retrieved.ExpiresAt).TotalMilliseconds) < 1);
    }

    [Fact]
    public async Task PasswordResetToken_DuplicateHash_ShouldThrowUniqueViolation()
    {
        // Arrange
        var user1 = UserAccount.Create(Email.Create($"ru1_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        var user2 = UserAccount.Create(Email.Create($"ru2_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        await _userRepository.AddAsync(user1);
        await _userRepository.AddAsync(user2);

        string sharedHash = "reset_hash_" + Guid.NewGuid().ToString("N");
        var now = DateTimeOffset.UtcNow;
        var token1 = PasswordResetToken.Create(user1.Id, sharedHash, now);
        var token2 = PasswordResetToken.Create(user2.Id, sharedHash, now);

        await _resetRepo.AddAsync(token1);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<PostgresException>(() => _resetRepo.AddAsync(token2));
        Assert.Equal("23505", ex.SqlState); // unique_violation (uq_password_reset_token_hash)
    }

    [Fact]
    public async Task TransactionalRegistration_WithRollback_ShouldPersistNothing()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"tx_rb_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        var credential = UserCredential.Create(user.Id, "argon_hash");
        var token = EmailVerificationToken.Create(user.Id, $"tx_hash_{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        // Act: Ejecutar dentro de una transacción que se aborta
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await _userRepository.AddAsync(user, transaction);
        await _credentialRepository.AddAsync(credential, transaction);
        await _verificationRepo.AddAsync(token, transaction);

        await transaction.RollbackAsync();

        // Assert: Ninguna entidad debe persistir
        Assert.Null(await _userRepository.GetByIdAsync(user.Id));
        Assert.Null(await _credentialRepository.GetByUserIdAsync(user.Id));
        Assert.Null(await _verificationRepo.GetByHashAsync(token.TokenHash));
    }

    [Fact]
    public async Task TransactionalRegistration_WithCommit_ShouldPersistAllEntitiesAtomically()
    {
        // Arrange
        var user = UserAccount.Create(Email.Create($"tx_cm_{Guid.NewGuid():N}@yusay.local"), DateTimeOffset.UtcNow.AddYears(-20));
        var credential = UserCredential.Create(user.Id, "argon_hash");
        var token = EmailVerificationToken.Create(user.Id, $"tx_cm_hash_{Guid.NewGuid():N}", DateTimeOffset.UtcNow);

        // Act: Ejecutar dentro de una transacción con commit
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await _userRepository.AddAsync(user, transaction);
        await _credentialRepository.AddAsync(credential, transaction);
        await _verificationRepo.AddAsync(token, transaction);

        await transaction.CommitAsync();

        // Assert: Todas las entidades deben persistir
        Assert.NotNull(await _userRepository.GetByIdAsync(user.Id));
        Assert.NotNull(await _credentialRepository.GetByUserIdAsync(user.Id));
        Assert.NotNull(await _verificationRepo.GetByHashAsync(token.TokenHash));
    }
}
