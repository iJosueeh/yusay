using Yusay.Application.Tracking.Queries.Dimensions;

namespace Yusay.Application.Tracking.Commands.UpdateDimensionVersion;

public interface IUpdateDimensionVersionUseCase
{
    Task<DimensionVersionResult> ExecuteAsync(
        UpdateDimensionVersionCommand command,
        CancellationToken cancellationToken = default);
}
