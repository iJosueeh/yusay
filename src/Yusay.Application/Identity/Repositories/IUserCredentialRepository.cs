using System.Data.Common;
using Yusay.Domain.Identity.Entities;

namespace Yusay.Application.Identity.Repositories;

public interface IUserCredentialRepository
{
    Task<UserCredential?> GetByUserIdAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task AddAsync(UserCredential credential, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task UpdateAsync(UserCredential credential, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
