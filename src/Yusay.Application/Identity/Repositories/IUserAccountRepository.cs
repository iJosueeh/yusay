using System.Data.Common;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;

namespace Yusay.Application.Identity.Repositories;

public interface IUserAccountRepository
{
    Task<UserAccount?> GetByIdAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<UserAccount?> GetByEmailAsync(Email email, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<bool> ExistsByEmailAsync(Email email, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task AddAsync(UserAccount userAccount, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserAccount userAccount, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
