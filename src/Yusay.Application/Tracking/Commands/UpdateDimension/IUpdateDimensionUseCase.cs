namespace Yusay.Application.Tracking.Commands.UpdateDimension;

public interface IUpdateDimensionUseCase
{
    Task<UpdateDimensionResult> ExecuteAsync(
        UpdateDimensionCommand command,
        CancellationToken cancellationToken = default);
}
