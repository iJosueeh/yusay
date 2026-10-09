namespace Yusay.Application.Common.Interfaces;

public interface IEmailVerificationSender
{
    Task SendVerificationTokenAsync(
        string recipientEmail,
        string verificationToken,
        CancellationToken cancellationToken = default);
}
