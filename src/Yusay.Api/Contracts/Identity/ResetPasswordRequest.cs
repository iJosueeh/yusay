namespace Yusay.Api.Contracts.Identity;

public sealed record ResetPasswordRequest(string Token, string NewPassword);
