using System.Data.Common;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Repositories;
using Yusay.Application.Identity.Tokens;
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

    /// <summary>Inyecta un fallo de persistencia en la próxima inserción (p. ej. PostgreSQL caído).</summary>
    public bool FailNextAdd { get; set; }

    public Task AddAsync(AuditEvent auditEvent, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (FailNextAdd)
        {
            FailNextAdd = false;
            throw new InvalidOperationException("Fallo inyectado de PostgreSQL al registrar la auditoría.");
        }

        Events.Add(auditEvent);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Denylist en memoria con la semántica SET NX de Redis: alta idempotente bajo concurrencia y
/// modo de indisponibilidad para ejercitar el fail-closed de la validación y del cierre.
/// </summary>
public sealed class FakeAccessTokenDenylist : IAccessTokenDenylist
{
    private readonly HashSet<string> _revoked = new(StringComparer.Ordinal);
    private readonly object _sync = new();

    public bool Unavailable { get; set; }

    public int RevokedCount
    {
        get
        {
            lock (_sync)
            {
                return _revoked.Count;
            }
        }
    }

    public Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();

        lock (_sync)
        {
            return Task.FromResult(_revoked.Contains(tokenId));
        }
    }

    public Task<bool> RevokeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        ThrowIfUnavailable();

        lock (_sync)
        {
            // Como SET ... NX: solo la primera llamada por jti recibe confirmación.
            return Task.FromResult(_revoked.Add(tokenId));
        }
    }

    private void ThrowIfUnavailable()
    {
        if (Unavailable)
        {
            throw new ServiceUnavailableException("El almacén de revocación de sesiones no está disponible (simulado).");
        }
    }
}

/// <summary>
/// Doble de <see cref="IEmailVerificationSender"/> que captura las entregas de tokens de
/// verificación para poder asertarlas sin incorporar un proveedor de correo externo.
/// </summary>
public sealed class FakeEmailVerificationSender : IEmailVerificationSender
{
    private readonly object _sync = new();

    public List<(string Email, string Token)> Deliveries { get; } = new();

    public Task SendVerificationTokenAsync(
        string recipientEmail,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            Deliveries.Add((recipientEmail, verificationToken));
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Doble de <see cref="ICurrentUser"/> para pruebas unitarias de casos de uso (F1 de N1):
/// fija la identidad corriente —o su ausencia con <c>null</c>— sin depender de ASP.NET Core.
/// </summary>
public sealed class FakeCurrentUser : ICurrentUser
{
    public FakeCurrentUser(Guid? userId = null)
    {
        UserId = userId;
    }

    public Guid? UserId { get; set; }
}
