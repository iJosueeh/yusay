namespace Yusay.Application.Content.Commands.CreateTopic;

public sealed record CreateTopicCommand(
    string Code,
    string Name,
    string? Description);
