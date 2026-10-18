using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Queries.ListPublishedInstruments;

public sealed class ListPublishedInstrumentsUseCase(
    ICurrentUser currentUser,
    IInstrumentCatalogRepository instrumentCatalogRepository) : IListPublishedInstrumentsUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IInstrumentCatalogRepository _instrumentCatalogRepository = instrumentCatalogRepository;

    public async Task<IReadOnlyList<PublishedInstrumentSummaryDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar el catálogo de instrumentos.");
        }

        return await _instrumentCatalogRepository.ListPublishedInstrumentsAsync(cancellationToken);
    }
}
