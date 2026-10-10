using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Emailing;

public sealed class NullPasswordResetEmailSender : IPasswordResetEmailSender
{
    public Task SendPasswordResetTokenAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
