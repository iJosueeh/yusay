using Yusay.Application.Tracking.Queries.GetTimeline;

namespace Yusay.Application.Tracking.Repositories;

public interface ITimelineRepository
{
    Task<IReadOnlyList<TimelineItemDto>> GetTimelineAsync(
        Guid userId,
        int limit,
        DateTimeOffset? cursorOccurredAt,
        string? cursorEventType,
        Guid? cursorEventId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? type,
        CancellationToken cancellationToken = default);
}
