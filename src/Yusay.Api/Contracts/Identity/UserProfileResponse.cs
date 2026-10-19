namespace Yusay.Api.Contracts.Identity;

public sealed record UserProfileResponse(
    Guid UserId,
    string Email,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    DateTimeOffset AdultConfirmedAt,
    string Status);
