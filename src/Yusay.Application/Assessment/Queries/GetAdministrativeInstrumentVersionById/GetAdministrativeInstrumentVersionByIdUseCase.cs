using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Enums;

namespace Yusay.Application.Assessment.Queries.GetAdministrativeInstrumentVersionById;

public interface IGetAdministrativeInstrumentVersionByIdUseCase
{
    Task<InstrumentVersionResult> ExecuteAsync(
        Guid instrumentId,
        Guid versionId,
        CancellationToken cancellationToken = default);
}

public sealed class GetAdministrativeInstrumentVersionByIdUseCase(
    ICurrentUser currentUser,
    IInstrumentAdministrationRepository instrumentAdministrationRepository) : IGetAdministrativeInstrumentVersionByIdUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;

    public async Task<InstrumentVersionResult> ExecuteAsync(
        Guid instrumentId,
        Guid versionId,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar el catálogo de Instrumentos.");
        }

        var instrument = await _instrumentAdministrationRepository.GetByIdAsync(instrumentId, cancellationToken);
        if (instrument is null)
        {
            throw new NotFoundException("El Instrumento solicitado no existe.");
        }

        var version = await _instrumentAdministrationRepository.GetVersionByIdAsync(versionId, cancellationToken);
        if (version is null || version.InstrumentId != instrumentId)
        {
            throw new NotFoundException("La versión solicitada no existe para este Instrumento.");
        }

        return new InstrumentVersionResult(
            version.InstrumentVersionId,
            version.InstrumentId,
            version.Version,
            version.Status.ToDbValue(),
            version.SourceDescription,
            version.Population,
            version.AdministrationConditions,
            version.LicenseInformation,
            version.Limitations);
    }
}
