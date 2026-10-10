using Dapper;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Identity.Repositories;

/// <summary>
/// Comprobación de habilitación administrativa consultando yusay.administrator por la
/// identidad ya validada (OQ-ARCH-017). Una única consulta por evaluación; el fallo de
/// conexión se propaga como excepción para que la capa de autorización aplique fail-closed.
/// </summary>
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
