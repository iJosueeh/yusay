using System.Data.Common;
using Yusay.Domain.Identity.Entities;

namespace Yusay.Application.Identity.Repositories;

public interface IEmailVerificationTokenRepository
{
    Task<EmailVerificationToken?> GetByHashAsync(string tokenHash, DbTransaction? transaction = null, bool forUpdate = false, CancellationToken cancellationToken = default);
    Task<EmailVerificationToken?> GetLatestByUserIdAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task AddAsync(EmailVerificationToken token, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task InvalidateAllForUserAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid verificationTokenId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
