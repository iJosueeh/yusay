using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Commands.RetireResource;

public interface IRetireResourceUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(RetireResourceCommand command, CancellationToken cancellationToken = default);
}
