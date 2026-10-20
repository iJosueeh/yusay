namespace Yusay.Application.Identity.Commands.DeleteUserAccount;

public sealed record DeleteUserAccountCommand(string Password, string? AccessToken = null);
