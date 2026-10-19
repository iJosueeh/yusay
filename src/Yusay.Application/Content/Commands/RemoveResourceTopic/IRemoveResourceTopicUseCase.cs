using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Commands.RemoveResourceTopic;

public interface IRemoveResourceTopicUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(RemoveResourceTopicCommand command, CancellationToken cancellationToken = default);
}
