using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Commands.CreateCheckIn;

public sealed class CreateCheckInUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICheckInRepository checkInRepository,
    IDimensionVersionRepository dimensionVersionRepository) : ICreateCheckInUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICheckInRepository _checkInRepository = checkInRepository;
    private readonly IDimensionVersionRepository _dimensionVersionRepository = dimensionVersionRepository;

    public async Task<CreateCheckInResult> ExecuteAsync(
        CreateCheckInCommand command,
        CancellationToken cancellationToken = default)
    {
        var ownerId = _currentUser.UserId;
        if (ownerId is null || ownerId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para crear un CheckIn.");
        }

        if (command.Measurements is null || command.Measurements.Count == 0)
        {
            throw new ValidationException("Un CheckIn debe contener al menos una Measurement.");
        }

        var duplicateDimension = command.Measurements
            .GroupBy(measurement => measurement.DimensionId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateDimension is not null)
        {
            throw new ValidationException(
                $"Un CheckIn no puede contener más de una Measurement de la Dimensión {duplicateDimension.Key}.");
        }

        var note = string.IsNullOrWhiteSpace(command.Note) ? null : command.Note;

        var checkIn = await _unitOfWork.ExecuteInTransactionAsync(async transaction =>
        {
            var createdAt = await _checkInRepository.GetDatabaseTimestampAsync(transaction, cancellationToken);

            var recordedAt = command.RecordedAt ?? createdAt;
            if (recordedAt < createdAt - TimeSpan.FromHours(168) || recordedAt > createdAt)
            {
                throw new ValidationException(
                    "recordedAt debe situarse dentro de la ventana de 168 horas anterior al instante de creación.");
            }

            var measurements = new List<Measurement>(command.Measurements.Count);
            foreach (var input in command.Measurements)
            {
                var scale = await _dimensionVersionRepository.GetActiveScaleAsync(
                    input.DimensionId, transaction, cancellationToken);

                if (scale is null)
                {
                    if (!await _dimensionVersionRepository.DimensionExistsAsync(
                            input.DimensionId, transaction, cancellationToken))
                    {
                        throw new NotFoundException($"No existe ninguna Dimensión con identificador {input.DimensionId}.");
                    }

                    throw new ConflictException(
                        $"La Dimensión {input.DimensionId} no tiene una versión de escala activa.");
                }

                ScaleValueValidation.EnsureReachable(
                    input.DimensionId, input.Value, scale.MinValue, scale.MaxValue, scale.Step);
                measurements.Add(Measurement.Create(input.DimensionId, scale.DimensionVersionId, input.Value));
            }

            var checkIn = CheckIn.Create(
                ownerId.Value,
                recordedAt,
                createdAt,
                note,
                measurements);

            await _checkInRepository.CreateAsync(checkIn, transaction, cancellationToken);
            return checkIn;
        }, cancellationToken);

        return new CreateCheckInResult(
            checkIn.CheckInId,
            checkIn.RecordedAt,
            checkIn.CreatedAt,
            checkIn.Revision);
    }
}
