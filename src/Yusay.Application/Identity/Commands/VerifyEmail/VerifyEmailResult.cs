namespace Yusay.Application.Identity.Commands.VerifyEmail;

public sealed record VerifyEmailResult(
    Guid UserId,
    string Email,
    DateTimeOffset VerifiedAt);
