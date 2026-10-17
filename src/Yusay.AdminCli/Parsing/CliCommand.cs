namespace Yusay.AdminCli.Parsing;

public enum CommandType
{
    Provision,
    Revoke,
    Verify,
    ListPending,
    Help
}

public sealed record CliCommand(
    CommandType Type,
    Guid? TargetUserId = null,
    string? OperatorIdentifier = null,
    string? TicketRef = null,
    Guid? EventId = null,
    string? StateDirectory = null);
