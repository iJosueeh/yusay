using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Application.Tracking.Commands.DeleteCheckIn;

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
            var deleted = await _checkInRepository.TryDeleteOwnedAsync(
                command.CheckInId, ownerId.Value, command.Revision.Value, transaction, cancellationToken);
            if (deleted)
            {
                return;
            }

            var actual = await _checkInRepository.GetByIdOwnedAsync(
            command.CheckInId, ownerId.Value, transaction, cancellationToken) ?? throw new NotFoundException("El CheckIn solicitado no existe.");

            throw new ConflictException(
                "La revisión esperada no coincide con la revisión vigente del CheckIn; vuelve a consultarlo antes de reintentar.");
        }, cancellationToken);
    }
}
