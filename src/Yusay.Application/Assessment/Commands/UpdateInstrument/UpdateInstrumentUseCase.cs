using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Assessment.Commands.UpdateInstrument;

public sealed class UpdateInstrumentUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInstrumentAdministrationRepository instrumentAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IUpdateInstrumentUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<InstrumentResult> ExecuteAsync(
        UpdateInstrumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Instrumentos.");
        }

        InstrumentResult? result = null;

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var instrument = await _instrumentAdministrationRepository.GetByIdForUpdateAsync(
                command.InstrumentId, tx, cancellationToken);

            if (instrument is null)
            {
                throw new NotFoundException("El Instrumento solicitado no existe.");
            }

            var newName = command.Name is not null ? command.Name.Trim() : instrument.Name;
            var newDescription = command.Description is not null ? command.Description.Trim() : instrument.Description;
            var newPurpose = command.Purpose is not null ? command.Purpose.Trim() : instrument.Purpose;

            var changedFields = new List<string>();
            if (command.Name is not null && !string.Equals(instrument.Name, newName, StringComparison.Ordinal))
            {
                changedFields.Add("name");
            }
            if (command.Description is not null && !string.Equals(instrument.Description, newDescription, StringComparison.Ordinal))
            {
                changedFields.Add("description");
            }
            if (command.Purpose is not null && !string.Equals(instrument.Purpose, newPurpose, StringComparison.Ordinal))
            {
                changedFields.Add("purpose");
            }

            if (changedFields.Count > 0)
            {
                instrument.UpdateDetails(newName, newDescription, newPurpose);
                await _instrumentAdministrationRepository.UpdateInstrumentAsync(instrument, tx, cancellationToken);

                var audit = AuditEvent.CreateInstrumentUpdated(
                    instrument.InstrumentId,
                    adminUserId.Value,
                    changedFields);

                await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
            }

            result = new InstrumentResult(
                instrument.InstrumentId,
                instrument.Code.Value,
                instrument.Name,
                instrument.Description,
                instrument.Purpose);
        }, cancellationToken);

        return result!;
    }
}
