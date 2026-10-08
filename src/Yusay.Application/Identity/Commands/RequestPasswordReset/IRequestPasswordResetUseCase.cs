namespace Yusay.Application.Identity.Commands.RequestPasswordReset;

public interface IRequestPasswordResetUseCase
{
    Task<RequestPasswordResetResult> ExecuteAsync(RequestPasswordResetCommand command, CancellationToken cancellationToken = default);
}
