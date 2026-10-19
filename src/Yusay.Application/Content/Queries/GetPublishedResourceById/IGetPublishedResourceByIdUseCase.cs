using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Queries.GetPublishedResourceById;

public interface IGetPublishedResourceByIdUseCase
{
    Task<ResourceDetailsDto> ExecuteAsync(Guid resourceId, CancellationToken cancellationToken = default);
}
