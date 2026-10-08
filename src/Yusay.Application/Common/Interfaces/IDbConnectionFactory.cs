using System.Data.Common;

namespace Yusay.Application.Common.Interfaces;

public interface IDbConnectionFactory
{
    Task<DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
    DbConnection CreateConnection();
}
