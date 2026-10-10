using System.Data.Common;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Application.Tracking.Repositories;

public interface ICheckInRepository
{
    Task<DateTimeOffset> GetDatabaseTimestampAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task CreateAsync(CheckIn checkIn, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<CheckIn?> GetByIdOwnedAsync(Guid checkInId, Guid ownerId, DbTransaction? transaction = null, CancellationToken cancellationToken = default);
    Task<bool> TryUpdateOwnedAsync(
        Guid checkInId,
        Guid ownerId,
        int expectedRevision,
        DateTimeOffset recordedAt,
        string? note,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
    Task<int> TryUpdateOwnedMeasurementsAsync(
        Guid checkInId,
        IReadOnlyList<Measurement> measurements,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// DELETE condicional atómico (OQ-DOM-009): filtra por check_in_id, propietario y
    /// revisión esperada en una única sentencia, sin ventana temporal. La cascada FK
    /// existente suprime Measurements y relaciones dependientes en la misma transacción.
    /// Devuelve false si ninguna fila condicionó (ajeno, inexistente o revisión vieja).
    /// </summary>
    Task<bool> TryDeleteOwnedAsync(
        Guid checkInId,
        Guid ownerId,
        int expectedRevision,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
