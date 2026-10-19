namespace Yusay.Application.Identity.Dtos;

public sealed record UserProfileDto(
    Guid UserId,
    string Email,
    bool EmailVerified,
    DateTimeOffset CreatedAt,
    DateTimeOffset AdultConfirmedAt,
    string Status);
