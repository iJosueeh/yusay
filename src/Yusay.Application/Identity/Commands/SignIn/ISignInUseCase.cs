namespace Yusay.Application.Identity.Commands.SignIn;

public interface ISignInUseCase
{
    Task<SignInResult> ExecuteAsync(SignInCommand command, CancellationToken cancellationToken = default);
}
