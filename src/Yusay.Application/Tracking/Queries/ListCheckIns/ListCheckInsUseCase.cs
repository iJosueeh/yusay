using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.GetCheckInById;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Queries.ListCheckIns;

public sealed class ListCheckInsUseCase(
    ICurrentUser currentUser,
    ICheckInRepository checkInRepository) : IListCheckInsUseCase
{
    public const int DefaultLimit = 20;
    public const int MaxLimit = 100;

    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ICheckInRepository _checkInRepository = checkInRepository;

    public async Task<ListCheckInsResult> ExecuteAsync(
        ListCheckInsQuery query,
        CancellationToken cancellationToken = default)
    {
        var ownerId = _currentUser.UserId;
        if (ownerId is null || ownerId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para consultar los CheckIns.");
        }

        var limit = query.Limit ?? DefaultLimit;
        if (limit is < 1 or > MaxLimit)
        {
            throw new ValidationException("El parámetro limit debe ser un entero entre 1 y 100.");
        }

        var cursor = CheckInListCursor.Decode(query.Cursor);

        var rows = await _checkInRepository.ListOwnedPageAsync(
            ownerId.Value,
            limit + 1,
            cursor?.RecordedAt,
            cursor?.CheckInId,
            cancellationToken);

        string? nextCursor = null;
        IReadOnlyList<GetCheckInByIdResult> items;

        if (rows.Count > limit)
        {
            items = rows.Take(limit).Select(ToResult).ToArray();
            nextCursor = CheckInListCursor.Encode(items[^1].RecordedAt, items[^1].CheckInId);
        }
        else
        {
            items = rows.Select(ToResult).ToArray();
        }

        return new ListCheckInsResult(items, nextCursor);
    }

    private static GetCheckInByIdResult ToResult(CheckIn checkIn) => new(
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
