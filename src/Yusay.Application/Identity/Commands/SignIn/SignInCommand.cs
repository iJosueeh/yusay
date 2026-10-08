namespace Yusay.Application.Identity.Commands.SignIn;

public sealed record SignInCommand(
    string Email,
    string Password);
