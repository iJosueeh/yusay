namespace Yusay.Application.Tracking.Commands.CreateCheckIn;

public interface ICreateCheckInUseCase
{
    Task<CreateCheckInResult> ExecuteAsync(CreateCheckInCommand command, CancellationToken cancellationToken = default);
}
