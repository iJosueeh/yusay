using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Queries.ListPublishedInstruments;

public interface IListPublishedInstrumentsUseCase
{
    Task<IReadOnlyList<PublishedInstrumentSummaryDto>> ExecuteAsync(CancellationToken cancellationToken = default);
}
