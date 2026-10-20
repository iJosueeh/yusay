using Yusay.Application.Common.Interfaces;

namespace Yusay.Infrastructure.Emailing;

public sealed class NullEmailVerificationSender : IEmailVerificationSender
{
    public Task SendVerificationTokenAsync(
        string recipientEmail,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n=======================================================");
        Console.WriteLine($"[DEV EMAIL] Token de verificación para: {recipientEmail}");
        Console.WriteLine($"TOKEN: {verificationToken}");
        Console.WriteLine($"=======================================================\n");

        return Task.CompletedTask;
    }
}
