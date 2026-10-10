namespace Yusay.Application.Common.Interfaces;

/// <summary>
/// Comprobación de habilitación administrativa (OQ-ARCH-017): la habilitación existe como
/// fila en yusay.administrator para la cuenta indicada. La consulta se ejecuta contra
/// PostgreSQL en cada evaluación administrativa, sin caché ni claims de rol en el JWT.
/// </summary>
public interface IAdministratorAuthorizationRepository
{
    Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken = default);
}
