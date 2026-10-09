namespace Yusay.Api.Contracts.Identity;

public sealed record ResetPasswordResponse(Guid UserId, string Email);
