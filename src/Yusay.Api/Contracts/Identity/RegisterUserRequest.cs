namespace Yusay.Api.Contracts.Identity;

public sealed record RegisterUserRequest(
    string Email,
    string Password,
    bool AdultConfirmed,
    DateTimeOffset? AdultConfirmedAt = null);
