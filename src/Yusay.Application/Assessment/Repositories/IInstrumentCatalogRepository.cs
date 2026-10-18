using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Repositories;

public interface IInstrumentCatalogRepository
{
    Task<IReadOnlyList<PublishedInstrumentSummaryDto>> ListPublishedInstrumentsAsync(
        CancellationToken cancellationToken = default);

    Task<PublishedInstrumentDetailsDto?> GetPublishedInstrumentDetailsAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);
}
