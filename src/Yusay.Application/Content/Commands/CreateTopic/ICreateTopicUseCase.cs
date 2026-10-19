using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Commands.CreateTopic;

public interface ICreateTopicUseCase
{
    Task<TopicDto> ExecuteAsync(CreateTopicCommand command, CancellationToken cancellationToken = default);
}
