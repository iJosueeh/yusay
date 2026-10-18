using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Queries.GetPublishedInstrumentById;

public sealed class GetPublishedInstrumentByIdUseCase(
    ICurrentUser currentUser,
    IInstrumentCatalogRepository instrumentCatalogRepository) : IGetPublishedInstrumentByIdUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IInstrumentCatalogRepository _instrumentCatalogRepository = instrumentCatalogRepository;

    public async Task<PublishedInstrumentDetailsDto> ExecuteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar el catálogo de instrumentos.");
        }

        var details = await _instrumentCatalogRepository.GetPublishedInstrumentDetailsAsync(
            instrumentId, cancellationToken);

        if (details is null)
        {
            throw new NotFoundException(
                "El instrumento solicitado no existe o no tiene una versión publicada.");
        }

        return details;
    }
}
