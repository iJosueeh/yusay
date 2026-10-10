namespace Yusay.Application.Common.Interfaces;

public interface IAdministratorAuthorizationRepository
{
    Task<bool> IsAdministratorAsync(Guid userId, CancellationToken cancellationToken = default);
}
