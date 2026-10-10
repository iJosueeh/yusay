namespace Yusay.Application.Tracking.Queries.ListCheckIns;

public interface IListCheckInsUseCase
{
    Task<ListCheckInsResult> ExecuteAsync(
        ListCheckInsQuery query,
        CancellationToken cancellationToken = default);
}
