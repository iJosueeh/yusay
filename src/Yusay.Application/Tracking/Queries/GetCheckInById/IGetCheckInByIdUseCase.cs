namespace Yusay.Application.Tracking.Queries.GetCheckInById;

public interface IGetCheckInByIdUseCase
{
    Task<GetCheckInByIdResult> ExecuteAsync(GetCheckInByIdQuery query, CancellationToken cancellationToken = default);
}
