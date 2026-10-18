using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Queries.GetPublishedInstrumentById;

public interface IGetPublishedInstrumentByIdUseCase
{
    Task<PublishedInstrumentDetailsDto> ExecuteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);
}
