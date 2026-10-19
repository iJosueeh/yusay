namespace Yusay.Application.Content.Queries.ListResources;

public sealed record ListResourcesQuery(
    string? Status = null,
    string? Type = null,
    Guid? TopicId = null);
