namespace Yusay.Application.Tracking.Commands.UpdateCheckIn;

public interface IUpdateCheckInUseCase
{
    Task<UpdateCheckInResult> ExecuteAsync(UpdateCheckInCommand command, CancellationToken cancellationToken = default);
}
