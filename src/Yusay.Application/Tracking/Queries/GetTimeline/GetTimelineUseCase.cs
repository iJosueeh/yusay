using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Application.Tracking.Queries.GetTimeline;

public sealed class GetTimelineUseCase(
    ICurrentUser currentUser,
    ITimelineRepository timelineRepository) : IGetTimelineUseCase
{
    public const int DefaultLimit = 20;
    public const int MinLimit = 1;
    public const int MaxLimit = 100;

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ITimelineRepository _timelineRepository = timelineRepository;

    public async Task<TimelineResult> ExecuteAsync(
        GetTimelineQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (!userId.HasValue)
        {
            throw new UnauthorizedException("Se requiere un usuario autenticado para consultar la línea de tiempo.");
        }

        var limit = query.Limit ?? DefaultLimit;
        if (limit < MinLimit || limit > MaxLimit)
        {
            throw new ValidationException("El parámetro limit debe ser un entero entre 1 y 100.");
        }

        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
        {
            throw new ValidationException("El parámetro 'from' no puede ser posterior al parámetro 'to'.");
        }

        string? normalizedType = null;
        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            normalizedType = query.Type.Trim().ToLowerInvariant();
            if (normalizedType != "assessment" && normalizedType != "check_in")
            {
                throw new ValidationException("El parámetro 'type' debe ser 'assessment' o 'check_in'.");
            }
        }

        var cursorKey = TimelineCursor.Decode(query.Cursor);

        var fetchLimit = limit + 1;
        var rawItems = await _timelineRepository.GetTimelineAsync(
            userId.Value,
            fetchLimit,
            cursorKey?.OccurredAt,
            cursorKey?.EventType,
            cursorKey?.EventId,
            query.From,
            query.To,
            normalizedType,
            cancellationToken);

        if (rawItems.Count > limit)
        {
            var pagedItems = rawItems.Take(limit).ToList();
            var lastItem = pagedItems[^1];
            var nextCursor = TimelineCursor.Encode(lastItem.OccurredAt, lastItem.Type, lastItem.EventId);
            return new TimelineResult(pagedItems, nextCursor);
        }

        return new TimelineResult(rawItems, null);
    }
}
