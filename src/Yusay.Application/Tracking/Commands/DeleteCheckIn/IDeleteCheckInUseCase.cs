namespace Yusay.Application.Tracking.Commands.DeleteCheckIn;

public interface IDeleteCheckInUseCase
{
    Task ExecuteAsync(DeleteCheckInCommand command, CancellationToken cancellationToken = default);
}
