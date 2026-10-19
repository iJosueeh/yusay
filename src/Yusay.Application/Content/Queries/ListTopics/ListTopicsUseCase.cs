using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;

namespace Yusay.Application.Content.Queries.ListTopics;

public sealed class ListTopicsUseCase(ITopicRepository topicRepository) : IListTopicsUseCase
{
    private readonly ITopicRepository _topicRepository = topicRepository;

    public async Task<IReadOnlyList<TopicDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var topics = await _topicRepository.ListAllAsync(cancellationToken);

        return topics.Select(t => new TopicDto(t.TopicId, t.Code.Value, t.Name, t.Description)).ToList();
    }
}
