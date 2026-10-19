using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Queries.ListTopics;

public interface IListTopicsUseCase
{
    Task<IReadOnlyList<TopicDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}
