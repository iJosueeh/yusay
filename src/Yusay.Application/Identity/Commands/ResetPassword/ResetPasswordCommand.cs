namespace Yusay.Application.Identity.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword);
