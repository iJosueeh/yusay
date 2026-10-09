namespace Yusay.Application.Common.Interfaces;

public interface IPasswordResetEmailSender
{
    Task SendPasswordResetTokenAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default);
}
