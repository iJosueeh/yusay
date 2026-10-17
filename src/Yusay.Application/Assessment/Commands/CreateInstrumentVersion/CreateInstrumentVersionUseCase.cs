using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Assessment.Commands.CreateInstrumentVersion;

public sealed class CreateInstrumentVersionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInstrumentAdministrationRepository instrumentAdministrationRepository,
    IAuditEventRepository auditEventRepository) : ICreateInstrumentVersionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<InstrumentVersionResult> ExecuteAsync(
        CreateInstrumentVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Instrumentos.");
        }

        InstrumentVersion? createdVersion = null;

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var instrument = await _instrumentAdministrationRepository.GetByIdForUpdateAsync(
                command.InstrumentId, tx, cancellationToken);

            if (instrument is null)
            {
                throw new NotFoundException("El Instrumento solicitado no existe.");
            }

            int versionNumber;
            if (command.Version.HasValue)
            {
                versionNumber = command.Version.Value;
                var exists = await _instrumentAdministrationRepository.VersionNumberExistsAsync(
                    command.InstrumentId, versionNumber, tx, cancellationToken);

                if (exists)
                {
                    throw new ConflictException(
                        IInstrumentAdministrationRepository.DuplicateVersionConflictMessage);
                }
            }
            else
            {
                versionNumber = await _instrumentAdministrationRepository.GetNextVersionNumberAsync(
                    command.InstrumentId, tx, cancellationToken);
            }

            createdVersion = InstrumentVersion.CreateDraft(
                command.InstrumentId,
                versionNumber,
                command.SourceDescription,
                command.Population,
                command.AdministrationConditions,
                command.LicenseInformation,
                command.Limitations);

            await _instrumentAdministrationRepository.AddVersionAsync(createdVersion, tx, cancellationToken);

            var audit = AuditEvent.CreateInstrumentVersionCreated(
                createdVersion.InstrumentVersionId,
                adminUserId.Value);

            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        return new InstrumentVersionResult(
            createdVersion!.InstrumentVersionId,
            createdVersion.InstrumentId,
            createdVersion.Version,
            createdVersion.Status.ToDbValue(),
            createdVersion.SourceDescription,
            createdVersion.Population,
            createdVersion.AdministrationConditions,
            createdVersion.LicenseInformation,
            createdVersion.Limitations);
    }
}
