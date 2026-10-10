using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Commands.UpdateCheckIn;

public sealed class UpdateCheckInUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    ICheckInRepository checkInRepository,
    IDimensionVersionRepository dimensionVersionRepository) : IUpdateCheckInUseCase
{
    private const string ImmutableDimensionSetMessage =
        "El conjunto de dimensiones de un CheckIn es inmutable: la solicitud debe incluir exactamente las dimensiones registradas.";

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly ICheckInRepository _checkInRepository = checkInRepository;
    private readonly IDimensionVersionRepository _dimensionVersionRepository = dimensionVersionRepository;

    public async Task<UpdateCheckInResult> ExecuteAsync(
        UpdateCheckInCommand command,
        CancellationToken cancellationToken = default)
    {
        var ownerId = _currentUser.UserId;
        if (ownerId is null || ownerId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para actualizar un CheckIn.");
        }

        if (command.Revision is null or < 1)
        {
            throw new ValidationException("La revisión esperada (revision) es obligatoria y debe ser positiva.");
        }

        if (command.RecordedAt is null)
        {
            throw new ValidationException("recordedAt es obligatorio en la actualización de un CheckIn.");
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

        var updatedCheckIn = await _unitOfWork.ExecuteInTransactionAsync(async transaction =>
        {
            var current = await _checkInRepository.GetByIdOwnedAsync(
                command.CheckInId, ownerId.Value, transaction, cancellationToken);
            if (current is null)
            {
                throw new NotFoundException("El CheckIn solicitado no existe.");
            }

            var storedByDimension = current.Measurements.ToDictionary(measurement => measurement.DimensionId);
            foreach (var input in command.Measurements)
            {
                if (!storedByDimension.TryGetValue(input.DimensionId, out var storedMeasurement))
                {
                    throw new ValidationException(ImmutableDimensionSetMessage);
                }

                var scale = await _dimensionVersionRepository.GetScaleByVersionIdAsync(
                    storedMeasurement.DimensionVersionId, transaction, cancellationToken);
                if (scale is null)
                {
                    throw new ConflictException(
                        $"La DimensionVersion registrada para la Dimensión {input.DimensionId} no existe.");
                }

                ScaleValueValidation.EnsureReachable(
                    input.DimensionId, input.Value, scale.MinValue, scale.MaxValue, scale.Step);
            }

            if (command.Measurements.Count != current.Measurements.Count)
            {
                throw new ValidationException(ImmutableDimensionSetMessage);
            }

            if (command.RecordedAt < current.CreatedAt - TimeSpan.FromHours(168) ||
                command.RecordedAt > current.CreatedAt)
            {
                throw new ValidationException(
                    "recordedAt debe situarse dentro de la ventana de 168 horas anterior a created_at.");
            }

            var applied = await _checkInRepository.TryUpdateOwnedAsync(
                command.CheckInId, ownerId.Value, command.Revision.Value,
                command.RecordedAt.Value, note, transaction, cancellationToken);

            if (!applied)
            {
                var actual = await _checkInRepository.GetByIdOwnedAsync(
                    command.CheckInId, ownerId.Value, transaction, cancellationToken);
                if (actual is null)
                {
                    throw new NotFoundException("El CheckIn solicitado no existe.");
                }

                var databaseNow = await _checkInRepository.GetDatabaseTimestampAsync(transaction, cancellationToken);
                if (databaseNow >= actual.CreatedAt + TimeSpan.FromHours(168))
                {
                    throw new ConflictException(
                        "La ventana de edición del CheckIn ha expirado: solo admite cambios durante las 168 horas posteriores a created_at.");
                }

                throw new ConflictException(
                    "La revisión esperada no coincide con la revisión vigente del CheckIn; vuelve a consultarlo antes de reintentar.");
            }

            var measurementUpdates = command.Measurements
                .Select(input => Measurement.Rehydrate(
                    input.DimensionId, storedByDimension[input.DimensionId].DimensionVersionId, input.Value))
                .ToArray();

            var updatedMeasurements = await _checkInRepository.TryUpdateOwnedMeasurementsAsync(
                command.CheckInId, measurementUpdates, transaction, cancellationToken);
            if (updatedMeasurements != measurementUpdates.Length)
            {
                throw new ConflictException("No se pudieron actualizar todas las mediciones del CheckIn.");
            }

            var refreshed = await _checkInRepository.GetByIdOwnedAsync(
                command.CheckInId, ownerId.Value, transaction, cancellationToken);
            if (refreshed is null)
            {
                throw new ConflictException("El CheckIn dejó de existir durante la actualización.");
            }

            return refreshed;
        }, cancellationToken);

        return new UpdateCheckInResult(
            updatedCheckIn.CheckInId,
            updatedCheckIn.RecordedAt,
            updatedCheckIn.CreatedAt,
            updatedCheckIn.UpdatedAt,
            updatedCheckIn.Revision,
            updatedCheckIn.Note,
            updatedCheckIn.Measurements
                .Select(measurement => new UpdateCheckInMeasurementResult(
                    measurement.DimensionId,
                    measurement.DimensionVersionId,
                    measurement.Value))
                .ToArray());
    }
}
