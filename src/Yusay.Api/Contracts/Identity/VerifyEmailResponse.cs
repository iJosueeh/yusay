namespace Yusay.Api.Contracts.Identity;

public sealed record VerifyEmailResponse(Guid UserId, string Email, DateTimeOffset VerifiedAt);
