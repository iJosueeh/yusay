namespace Yusay.Application.Identity.Commands.DeleteUserAccount;

public interface IDeleteUserAccountUseCase
{
    Task ExecuteAsync(DeleteUserAccountCommand command, CancellationToken cancellationToken = default);
}
