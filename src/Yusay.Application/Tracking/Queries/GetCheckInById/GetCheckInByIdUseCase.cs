using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;

namespace Yusay.Application.Tracking.Queries.GetCheckInById;

public sealed class GetCheckInByIdUseCase(
    ICurrentUser currentUser,
    ICheckInRepository checkInRepository) : IGetCheckInByIdUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICheckInRepository _checkInRepository = checkInRepository;

    public async Task<GetCheckInByIdResult> ExecuteAsync(
        GetCheckInByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        var ownerId = _currentUser.UserId;
        if (ownerId is null || ownerId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para consultar un CheckIn.");
        }

        var checkIn = await _checkInRepository.GetByIdOwnedAsync(
            query.CheckInId, ownerId.Value, transaction: null, cancellationToken);

        if (checkIn is null)
        {
            throw new NotFoundException("El CheckIn solicitado no existe.");
        }

        return new GetCheckInByIdResult(
            checkIn.CheckInId,
            checkIn.RecordedAt,
            checkIn.CreatedAt,
            checkIn.UpdatedAt,
            checkIn.Revision,
            checkIn.Note,
            checkIn.Measurements
                .Select(measurement => new GetCheckInMeasurementResult(
                    measurement.DimensionId,
                    measurement.DimensionVersionId,
                    measurement.Value))
                .ToArray());
    }
}
