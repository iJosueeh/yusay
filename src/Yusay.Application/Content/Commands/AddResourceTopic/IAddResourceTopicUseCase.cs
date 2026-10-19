using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Commands.AddResourceTopic;

public interface IAddResourceTopicUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(AddResourceTopicCommand command, CancellationToken cancellationToken = default);
}
