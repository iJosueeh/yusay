using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Emailing;

public sealed class NullEmailVerificationSender : IEmailVerificationSender
{
    public Task SendVerificationTokenAsync(
        string recipientEmail,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
