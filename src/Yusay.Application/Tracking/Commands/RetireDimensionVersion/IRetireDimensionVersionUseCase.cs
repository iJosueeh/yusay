using Yusay.Application.Tracking.Queries.Dimensions;

namespace Yusay.Application.Tracking.Commands.RetireDimensionVersion;

public interface IRetireDimensionVersionUseCase
{
    Task<DimensionVersionResult> ExecuteAsync(
        RetireDimensionVersionCommand command,
        CancellationToken cancellationToken = default);
}
