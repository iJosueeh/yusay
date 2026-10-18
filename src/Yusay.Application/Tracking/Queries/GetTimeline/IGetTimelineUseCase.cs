namespace Yusay.Application.Tracking.Queries.GetTimeline;

public interface IGetTimelineUseCase
{
    Task<TimelineResult> ExecuteAsync(GetTimelineQuery query, CancellationToken cancellationToken = default);
}
