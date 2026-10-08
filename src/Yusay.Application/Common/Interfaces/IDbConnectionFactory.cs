namespace Yusay.Application.Common.Interfaces;

public interface IDbConnectionFactory
{
    Task<System.Data.Common.DbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
    System.Data.Common.DbConnection CreateConnection();
}
