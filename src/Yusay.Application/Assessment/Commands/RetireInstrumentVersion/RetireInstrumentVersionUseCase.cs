using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Assessment.Commands.RetireInstrumentVersion;

public sealed class RetireInstrumentVersionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInstrumentAdministrationRepository instrumentAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IRetireInstrumentVersionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<InstrumentVersionResult> ExecuteAsync(
        RetireInstrumentVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Instrumentos.");
        }

        InstrumentVersionResult? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var version = await _instrumentAdministrationRepository.GetVersionByIdForUpdateAsync(
                command.VersionId, tx, cancellationToken);

            if (version is null || version.InstrumentId != command.InstrumentId)
            {
                throw new NotFoundException("La versión solicitada no existe para este Instrumento.");
            }

            if (version.Status == InstrumentVersionStatus.Retired)
            {
                result = new InstrumentVersionResult(
                    version.InstrumentVersionId,
                    version.InstrumentId,
                    version.Version,
                    version.Status.ToDbValue(),
                    version.SourceDescription,
                    version.Population,
                    version.AdministrationConditions,
                    version.LicenseInformation,
                    version.Limitations);
                return;
            }

            if (version.Status != InstrumentVersionStatus.Published)
            {
                throw new ConflictException(
                    $"Transición de estado inválida: no se puede retirar una versión en estado {version.Status.ToDbValue()}. Solo PUBLISHED puede pasar a RETIRED.");
            }

            version.Retire();

            await _instrumentAdministrationRepository.UpdateVersionStatusAsync(
                version.InstrumentVersionId,
                InstrumentVersionStatus.Retired,
                tx,
                cancellationToken);

            var audit = AuditEvent.CreateInstrumentVersionRetired(
                version.InstrumentVersionId,
                adminUserId.Value);

            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            result = new InstrumentVersionResult(
                version.InstrumentVersionId,
                version.InstrumentId,
                version.Version,
                version.Status.ToDbValue(),
                version.SourceDescription,
                version.Population,
                version.AdministrationConditions,
                version.LicenseInformation,
                version.Limitations);
        }, cancellationToken);

        return result!;
    }
}
