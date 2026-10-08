using System.Data.Common;

namespace Yusay.Application.Common.Interfaces;

public interface IUnitOfWork
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<DbTransaction, Task<T>> operation,
        CancellationToken cancellationToken = default);

    Task ExecuteInTransactionAsync(
        Func<DbTransaction, Task> operation,
        CancellationToken cancellationToken = default);
}
