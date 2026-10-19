using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Commands.CreateResource;

public interface ICreateResourceUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(CreateResourceCommand command, CancellationToken cancellationToken = default);
}
