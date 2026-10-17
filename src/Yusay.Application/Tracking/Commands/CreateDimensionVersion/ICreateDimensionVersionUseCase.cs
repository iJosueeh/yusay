using Yusay.Application.Tracking.Queries.Dimensions;

namespace Yusay.Application.Tracking.Commands.CreateDimensionVersion;

public interface ICreateDimensionVersionUseCase
{
    Task<DimensionVersionResult> ExecuteAsync(
        CreateDimensionVersionCommand command,
        CancellationToken cancellationToken = default);
}
