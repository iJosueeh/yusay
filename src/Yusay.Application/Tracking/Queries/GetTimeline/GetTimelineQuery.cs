namespace Yusay.Application.Tracking.Queries.GetTimeline;

public sealed record GetTimelineQuery(
    int? Limit,
    string? Cursor,
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Type);
