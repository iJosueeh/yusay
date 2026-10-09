namespace Yusay.Application.Identity.Commands.SignOut;

public interface ISignOutUseCase
{
    Task<SignOutResult> ExecuteAsync(SignOutCommand command, CancellationToken cancellationToken = default);
}
