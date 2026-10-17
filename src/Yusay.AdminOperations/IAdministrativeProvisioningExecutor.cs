using Yusay.AdminOperations.Contracts;

namespace Yusay.AdminOperations;

public interface IAdministrativeProvisioningExecutor
{
    Task<AdministrativeProvisioningResult> ExecuteAsync(
        AdministrativeProvisioningRequest request,
        CancellationToken cancellationToken = default);
}
