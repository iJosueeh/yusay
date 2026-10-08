namespace Yusay.Application.Identity.Commands.VerifyEmail;

public interface IVerifyEmailUseCase
{
    Task<VerifyEmailResult> ExecuteAsync(VerifyEmailCommand command, CancellationToken cancellationToken = default);
}
