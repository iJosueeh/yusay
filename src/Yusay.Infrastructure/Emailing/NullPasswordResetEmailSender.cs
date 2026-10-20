using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Emailing;

public sealed class NullPasswordResetEmailSender : IPasswordResetEmailSender
{
    public Task SendPasswordResetTokenAsync(
        string recipientEmail,
        string resetToken,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n=======================================================");
        Console.WriteLine($"[DEV EMAIL] Token de recuperación para: {recipientEmail}");
        Console.WriteLine($"TOKEN: {resetToken}");
        Console.WriteLine($"=======================================================\n");

        return Task.CompletedTask;
    }
}
