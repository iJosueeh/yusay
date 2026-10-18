using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Assessment.Commands.PublishInstrumentVersion;

public sealed class PublishInstrumentVersionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInstrumentAdministrationRepository instrumentAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IPublishInstrumentVersionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<InstrumentVersionResult> ExecuteAsync(
        PublishInstrumentVersionCommand command,
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

            if (version.Status == InstrumentVersionStatus.Published)
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

            if (version.Status != InstrumentVersionStatus.Ready)
            {
                throw new ConflictException(
                    $"Transición de estado inválida: no se puede publicar una versión en estado {version.Status.ToDbValue()}. Solo READY puede pasar a PUBLISHED.");
            }

            version.Publish();

            await _instrumentAdministrationRepository.UpdateVersionStatusAsync(
                version.InstrumentVersionId,
                InstrumentVersionStatus.Published,
                tx,
                cancellationToken);

            var audit = AuditEvent.CreateInstrumentVersionPublished(
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
