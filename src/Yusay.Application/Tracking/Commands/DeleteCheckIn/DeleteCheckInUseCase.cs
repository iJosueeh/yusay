using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Application.Tracking.Commands.DeleteCheckIn;

/// <summary>
/// Eliminación de un CheckIn propio según OQ-DOM-009 / RN-022 / RF-011: propiedad
/// exclusiva desde <see cref="ICurrentUser"/>, <c>revision</c> esperada obligatoria y
/// eliminación física en cualquier momento —sin ventana de 168 horas— mediante una única
/// sentencia condicional que filtra por identificador, propietario y revisión; la cascada
/// FK existente suprime Measurements y relaciones dependientes. Ajeno, inexistente o ya
/// eliminado devuelven 404 uniforme; el 409 se diagnostica únicamente tras verificar la
/// propiedad. No genera <c>audit_event</c> (V011 no contempla operaciones de CheckIn).
/// </summary>
public sealed class DeleteCheckInUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICheckInRepository checkInRepository) : IDeleteCheckInUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICheckInRepository _checkInRepository = checkInRepository;

    public async Task ExecuteAsync(
        DeleteCheckInCommand command,
        CancellationToken cancellationToken = default)
    {
        var ownerId = _currentUser.UserId;
        if (ownerId is null || ownerId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para eliminar un CheckIn.");
        }

        if (command.Revision is null or < 1)
        {
            throw new ValidationException("La revisión esperada (revision) es obligatoria y debe ser positiva.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async transaction =>
        {
            // Sentencia condicional única: check_in_id + user_id + revision, sin ningún
            // predicado temporal (la eliminación está permitida en cualquier momento).
            var deleted = await _checkInRepository.TryDeleteOwnedAsync(
                command.CheckInId, ownerId.Value, command.Revision.Value, transaction, cancellationToken);
            if (deleted)
            {
                // Las cascadas FK (measurement, check_in_context_tag) se ejecutan dentro
                // de esta misma transacción de base de datos.
                return;
            }

            // Diagnóstico tras 0 filas (MP-PHYS-007): la propiedad se verifica antes de
            // cualquier 409, de modo que un recurso ajeno nunca produce conflicto.
            var actual = await _checkInRepository.GetByIdOwnedAsync(
                command.CheckInId, ownerId.Value, transaction, cancellationToken);
            if (actual is null)
            {
                // Ajeno, inexistente o ya eliminado: 404 uniforme e indistinguible.
                throw new NotFoundException("El CheckIn solicitado no existe.");
            }

            throw new ConflictException(
                "La revisión esperada no coincide con la revisión vigente del CheckIn; vuelve a consultarlo antes de reintentar.");
        }, cancellationToken);
    }
}
