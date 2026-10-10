using Dapper;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Identity.Repositories;

public sealed class AdministratorAuthorizationRepository(IDbConnectionFactory connectionFactory)
    : IAdministratorAuthorizationRepository
{
    public async Task<bool> IsAdministratorAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var result = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            """
            SELECT 1 FROM yusay.administrator WHERE user_id = @UserId
            """,
            new { UserId = userId },
            cancellationToken: cancellationToken));

        return result is not null;
    }
}
