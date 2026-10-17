using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;

namespace Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentById;

public interface IGetAdministrativeInstrumentByIdUseCase
{
    Task<InstrumentDetailsDto> ExecuteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default);
}

public sealed class GetAdministrativeInstrumentByIdUseCase(
    ICurrentUser currentUser,
    IInstrumentAdministrationRepository instrumentAdministrationRepository) : IGetAdministrativeInstrumentByIdUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;

    public async Task<InstrumentDetailsDto> ExecuteAsync(
        Guid instrumentId,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar el catálogo de Instrumentos.");
        }

        var instrument = await _instrumentAdministrationRepository.GetAdministrativeInstrumentDetailsAsync(
            instrumentId, cancellationToken);

        if (instrument is null)
        {
            throw new NotFoundException("El Instrumento solicitado no existe.");
        }

        return instrument;
    }
}
