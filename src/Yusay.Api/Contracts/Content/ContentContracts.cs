namespace Yusay.Api.Contracts.Content;

public sealed record TopicResponse(
    Guid TopicId,
    string Code,
    string Name,
    string? Description);

public sealed record ListTopicsResponse(
    IReadOnlyList<TopicResponse> Items);

public sealed record ResourceTopicResponse(
    Guid TopicId,
    string Code,
    string Name);

public sealed record ResourceDetailsResponse(
    Guid ResourceId,
    string Type,
    string Status,
    string Title,
    string? Summary,
    string? Body,
    string? ExternalUrl,
    IReadOnlyList<ResourceTopicResponse> Topics);

public sealed record ResourceSummaryResponse(
    Guid ResourceId,
    string Type,
    string Status,
    string Title,
    string? Summary,
    int TopicCount,
    IReadOnlyList<string> TopicCodes);

public sealed record ListResourcesResponse(
    IReadOnlyList<ResourceSummaryResponse> Items,
    string? NextCursor);
