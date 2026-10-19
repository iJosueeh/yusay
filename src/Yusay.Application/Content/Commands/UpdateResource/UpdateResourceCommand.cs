namespace Yusay.Application.Content.Commands.UpdateResource;

public sealed record UpdateResourceCommand(
    Guid ResourceId,
    string? Type,
    string Title,
    string? Summary,
    string? Body,
    string? ExternalUrl,
    string? CorrectionKind);
