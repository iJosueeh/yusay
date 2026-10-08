namespace Yusay.Application.Identity.Commands.RegisterUser;

public sealed record RegisterUserResult(
    Guid UserId,
    string Email,
    string VerificationToken);
