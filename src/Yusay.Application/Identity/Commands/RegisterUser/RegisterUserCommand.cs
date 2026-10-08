namespace Yusay.Application.Identity.Commands.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    bool AdultConfirmed,
    DateTimeOffset? AdultConfirmedAt = null);
