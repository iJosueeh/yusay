using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Queries.GetResourceById;

public interface IGetResourceByIdUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(Guid resourceId, CancellationToken cancellationToken = default);
}
