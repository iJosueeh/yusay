namespace Yusay.Application.Content.Dtos;

public sealed record TopicDto(
    Guid TopicId,
    string Code,
    string Name,
    string? Description);

public sealed record ResourceTopicDto(
    Guid TopicId,
    string Code,
    string Name);

public sealed record ResourceDetailsDto(
    Guid ResourceId,
    string Type,
    string Status,
    string Title,
    string? Summary,
    string? Body,
    string? ExternalUrl,
    IReadOnlyList<ResourceTopicDto> Topics);

public sealed record ResourceSummaryDto(
    Guid ResourceId,
    string Type,
    string Status,
    string Title,
    string? Summary,
    int TopicCount,
    IReadOnlyList<string> TopicCodes);

public sealed record ListResourcesResult(
    IReadOnlyList<ResourceSummaryDto> Items,
    string? NextCursor);
