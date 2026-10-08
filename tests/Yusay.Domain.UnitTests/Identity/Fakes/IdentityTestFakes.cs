using System.Data.Common;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Domain.UnitTests.Identity.Fakes;

public sealed class FakeUnitOfWork : IUnitOfWork
{
    public bool TransactionExecuted { get; private set; }

    public async Task<T> ExecuteInTransactionAsync<T>(Func<DbTransaction, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        TransactionExecuted = true;
        return await operation(null!);
    }

    public async Task ExecuteInTransactionAsync(Func<DbTransaction, Task> operation, CancellationToken cancellationToken = default)
    {
        TransactionExecuted = true;
        await operation(null!);
    }
}

public sealed class FakeUserAccountRepository : IUserAccountRepository
{
    public List<UserAccount> Users { get; } = new();

    public Task AddAsync(UserAccount userAccount, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Users.Add(userAccount);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Users.RemoveAll(u => u.Id == id);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsByEmailAsync(Email email, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Users.Any(u => string.Equals(u.Email.Value, email.Value, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<UserAccount?> GetByEmailAsync(Email email, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Users.FirstOrDefault(u => string.Equals(u.Email.Value, email.Value, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<UserAccount?> GetByIdAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Users.FirstOrDefault(u => u.Id == id));
    }

    public Task UpdateAsync(UserAccount userAccount, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var idx = Users.FindIndex(u => u.Id == userAccount.Id);
        if (idx >= 0) Users[idx] = userAccount;
        return Task.CompletedTask;
    }
}

public sealed class FakeUserCredentialRepository : IUserCredentialRepository
{
    public List<UserCredential> Credentials { get; } = new();

    public Task AddAsync(UserCredential credential, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Credentials.Add(credential);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Credentials.RemoveAll(c => c.UserId == userId);
        return Task.CompletedTask;
    }

    public Task<UserCredential?> GetByUserIdAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Credentials.FirstOrDefault(c => c.UserId == userId));
    }

    public Task UpdateAsync(UserCredential credential, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        var idx = Credentials.FindIndex(c => c.UserId == credential.UserId);
        if (idx >= 0) Credentials[idx] = credential;
        return Task.CompletedTask;
    }
}

public sealed class FakeEmailVerificationTokenRepository : IEmailVerificationTokenRepository
{
    public List<EmailVerificationToken> Tokens { get; } = new();

    public Task AddAsync(EmailVerificationToken token, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid verificationTokenId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Tokens.RemoveAll(t => t.VerificationTokenId == verificationTokenId);
        return Task.CompletedTask;
    }

    public Task<EmailVerificationToken?> GetByHashAsync(string tokenHash, DbTransaction? transaction = null, bool forUpdate = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));
    }

    public Task<EmailVerificationToken?> GetLatestByUserIdAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Tokens.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).FirstOrDefault());
    }

    public Task InvalidateAllForUserAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Tokens.RemoveAll(t => t.UserId == userId);
        return Task.CompletedTask;
    }
}

public sealed class FakePasswordResetTokenRepository : IPasswordResetTokenRepository
{
    public List<PasswordResetToken> Tokens { get; } = new();

    public Task AddAsync(PasswordResetToken token, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Tokens.Add(token);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid resetTokenId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Tokens.RemoveAll(t => t.ResetTokenId == resetTokenId);
        return Task.CompletedTask;
    }

    public Task<PasswordResetToken?> GetByHashAsync(string tokenHash, DbTransaction? transaction = null, bool forUpdate = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Tokens.FirstOrDefault(t => t.TokenHash == tokenHash));
    }

    public Task<PasswordResetToken?> GetLatestByUserIdAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Tokens.Where(t => t.UserId == userId).OrderByDescending(t => t.CreatedAt).FirstOrDefault());
    }

    public Task InvalidateAllForUserAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Tokens.RemoveAll(t => t.UserId == userId);
        return Task.CompletedTask;
    }
}

public sealed class FakeAuditEventRepository : IAuditEventRepository
{
    public List<AuditEvent> Events { get; } = new();

    public Task AddAsync(AuditEvent auditEvent, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}
