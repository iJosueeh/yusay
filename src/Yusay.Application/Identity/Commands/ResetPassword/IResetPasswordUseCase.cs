namespace Yusay.Application.Identity.Commands.ResetPassword;

public interface IResetPasswordUseCase
{
    Task<ResetPasswordResult> ExecuteAsync(ResetPasswordCommand command, CancellationToken cancellationToken = default);
}
