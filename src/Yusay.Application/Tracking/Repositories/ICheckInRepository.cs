using System.Data.Common;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Repositories;

public interface ICheckInRepository
{
    Task<DateTimeOffset> GetDatabaseTimestampAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task CreateAsync(CheckIn checkIn, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetByIdOwnedAsync(Guid checkInId, Guid ownerId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
}
