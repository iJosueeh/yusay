namespace Yusay.Application.Content.Commands.CreateResource;

public sealed record CreateResourceCommand(
    string Type,
    string Title,
    string? Summary,
    string? Body,
    string? ExternalUrl,
    IReadOnlyList<Guid>? TopicIds);
