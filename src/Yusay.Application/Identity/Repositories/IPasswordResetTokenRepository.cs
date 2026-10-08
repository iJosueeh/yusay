using System.Data.Common;
using Yusay.Domain.Identity.Entities;

namespace Yusay.Application.Identity.Repositories;

public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, DbTransaction? transaction = null, bool forUpdate = false, CancellationToken cancellationToken = default);
    Task<PasswordResetToken?> GetLatestByUserIdAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task AddAsync(PasswordResetToken token, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task InvalidateAllForUserAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid resetTokenId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
