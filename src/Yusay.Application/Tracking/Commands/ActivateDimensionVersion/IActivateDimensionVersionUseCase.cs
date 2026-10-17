using Yusay.Application.Tracking.Queries.Dimensions;

namespace Yusay.Application.Tracking.Commands.ActivateDimensionVersion;

public interface IActivateDimensionVersionUseCase
{
    Task<DimensionVersionResult> ExecuteAsync(
        ActivateDimensionVersionCommand command,
        CancellationToken cancellationToken = default);
}
