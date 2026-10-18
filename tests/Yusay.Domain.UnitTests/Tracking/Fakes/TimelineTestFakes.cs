using Yusay.Application.Tracking.Queries.GetTimeline;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Domain.UnitTests.Tracking.Fakes;

public sealed class FakeTimelineRepository : ITimelineRepository
{
    public List<(Guid UserId, TimelineItemDto Item)> Items { get; } = new();

    public Task<IReadOnlyList<TimelineItemDto>> GetTimelineAsync(
        Guid userId,
        int limit,
        DateTimeOffset? cursorOccurredAt,
        string? cursorEventType,
        Guid? cursorEventId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        string? type,
        CancellationToken cancellationToken = default)
    {
        var filtered = Items
            .Where(x => x.UserId == userId)
            .ToList();

        if (from.HasValue)
        {
            filtered = filtered.Where(x => x.Item.OccurredAt >= from.Value).ToList();
        }

        if (to.HasValue)
        {
            filtered = filtered.Where(x => x.Item.OccurredAt <= to.Value).ToList();
        }

        if (!string.IsNullOrEmpty(type))
        {
            filtered = filtered.Where(x => x.Item.Type == type).ToList();
        }

        filtered = filtered
            .OrderByDescending(x => x.Item.OccurredAt)
            .ThenByDescending(x => x.Item.Type, StringComparer.Ordinal)
            .ThenByDescending(x => x.Item.EventId.ToString("D"), StringComparer.Ordinal)
            .ToList();

        if (cursorOccurredAt.HasValue && !string.IsNullOrEmpty(cursorEventType) && cursorEventId.HasValue)
        {
            var cursorTime = cursorOccurredAt.Value;
            var cursorType = cursorEventType;
            var cursorIdStr = cursorEventId.Value.ToString("D");

            filtered = filtered.Where(x =>
            {
                if (x.Item.OccurredAt < cursorTime)
                {
                    return true;
                }

                if (x.Item.OccurredAt == cursorTime)
                {
                    var typeCmp = string.CompareOrdinal(x.Item.Type, cursorType);
                    if (typeCmp < 0)
                    {
                        return true;
                    }

                    if (typeCmp == 0)
                    {
                        return string.CompareOrdinal(x.Item.EventId.ToString("D"), cursorIdStr) < 0;
                    }
                }

                return false;
            }).ToList();
        }

        var result = filtered
            .Take(limit)
            .Select(x => x.Item)
            .ToList();

        return Task.FromResult<IReadOnlyList<TimelineItemDto>>(result);
    }
}
