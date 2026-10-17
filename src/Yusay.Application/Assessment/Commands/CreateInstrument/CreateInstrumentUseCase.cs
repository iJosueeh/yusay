using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.ValueObjects;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Assessment.Commands.CreateInstrument;

public sealed class CreateInstrumentUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IInstrumentAdministrationRepository instrumentAdministrationRepository,
    IAuditEventRepository auditEventRepository) : ICreateInstrumentUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IInstrumentAdministrationRepository _instrumentAdministrationRepository =
        instrumentAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<InstrumentResult> ExecuteAsync(
        CreateInstrumentCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Instrumentos.");
        }

        var code = InstrumentCode.Create(command.Code);
        var instrument = Instrument.Create(code, command.Name, command.Description, command.Purpose);

        var audit = AuditEvent.CreateInstrumentCreated(instrument.InstrumentId, adminUserId.Value);

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var exists = await _instrumentAdministrationRepository.CodeExistsAsync(code.Value, cancellationToken);
            if (exists)
            {
                throw new ConflictException(IInstrumentAdministrationRepository.DuplicateCodeConflictMessage);
            }

            await _instrumentAdministrationRepository.AddInstrumentAsync(instrument, tx, cancellationToken);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        return new InstrumentResult(
            instrument.InstrumentId,
            instrument.Code.Value,
            instrument.Name,
            instrument.Description,
            instrument.Purpose);
    }
}
