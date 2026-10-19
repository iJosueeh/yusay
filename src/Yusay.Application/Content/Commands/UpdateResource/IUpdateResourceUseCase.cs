using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Commands.UpdateResource;

public interface IUpdateResourceUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(UpdateResourceCommand command, CancellationToken cancellationToken = default);
}
