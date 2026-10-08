namespace Yusay.Application.Identity.Commands.RequestPasswordReset;

public sealed record RequestPasswordResetResult(
    bool EmailSent,
    string? ResetToken);
