namespace Yusay.Application.Content.Queries.ListPublishedResources;

public sealed record ListPublishedResourcesQuery(
    Guid? TopicId = null,
    Guid? DimensionId = null,
    Guid? AttemptId = null,
    string? Type = null,
    int? Limit = null,
    string? Cursor = null);
