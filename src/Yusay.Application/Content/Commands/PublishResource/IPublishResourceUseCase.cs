using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Commands.PublishResource;

public interface IPublishResourceUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(PublishResourceCommand command, CancellationToken cancellationToken = default);
}
