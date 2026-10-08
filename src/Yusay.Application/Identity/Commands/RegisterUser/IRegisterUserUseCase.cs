namespace Yusay.Application.Identity.Commands.RegisterUser;

public interface IRegisterUserUseCase
{
    Task<RegisterUserResult> ExecuteAsync(RegisterUserCommand command, CancellationToken cancellationToken = default);
}
