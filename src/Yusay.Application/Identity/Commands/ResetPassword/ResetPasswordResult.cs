namespace Yusay.Application.Identity.Commands.ResetPassword;

public sealed record ResetPasswordResult(
    Guid UserId,
    string Email,
    DateTimeOffset PasswordChangedAt);
