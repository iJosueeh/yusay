namespace Yusay.Application.Tracking.Commands.CreateDimension;

public interface ICreateDimensionUseCase
{
    Task<CreateDimensionResult> ExecuteAsync(
        CreateDimensionCommand command,
        CancellationToken cancellationToken = default);
}
