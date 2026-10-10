using System.Data.Common;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;

namespace Yusay.Domain.UnitTests.Tracking.Fakes;

public sealed class FakeCheckInRepository : ICheckInRepository
{
    public List<CheckIn> CheckIns { get; } = new();

    /// <summary>Instante fijo devuelto por GetDatabaseTimestampAsync para tests deterministas.</summary>
    public DateTimeOffset DatabaseTimestamp { get; set; } = new DateTimeOffset(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Simula fallo de persistencia (p. ej. violación física) dentro de la transacción.</summary>
    public Exception? CreateFailure { get; set; }

    public Task<DateTimeOffset> GetDatabaseTimestampAsync(DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DatabaseTimestamp);
    }

    public Task CreateAsync(CheckIn checkIn, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        if (CreateFailure is not null)
        {
            throw CreateFailure;
        }

        CheckIns.Add(checkIn);
        return Task.CompletedTask;
    }

    public Task<CheckIn?> GetByIdOwnedAsync(Guid checkInId, Guid ownerId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CheckIns.FirstOrDefault(c => c.CheckInId == checkInId && c.UserId == ownerId));
    }

    /// <summary>Simula fallo físico de escritura dentro de la transacción de actualización.</summary>
    public Exception? UpdateFailure { get; set; }

    public Task<bool> TryUpdateOwnedAsync(
        Guid checkInId,
        Guid ownerId,
        int expectedRevision,
        DateTimeOffset recordedAt,
        string? note,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (UpdateFailure is not null)
        {
            throw UpdateFailure;
        }

        // Réplica del predicado SQL de MP-PHYS-007: propiedad + revisión esperada +
        // ventana absoluta de 168 horas con límite superior estricto.
        var index = CheckIns.FindIndex(c => c.CheckInId == checkInId && c.UserId == ownerId);
        if (index < 0)
        {
            return Task.FromResult(false);
        }

        var row = CheckIns[index];
        if (row.Revision != expectedRevision || DatabaseTimestamp >= row.CreatedAt + TimeSpan.FromHours(168))
        {
            return Task.FromResult(false);
        }

        CheckIns[index] = CheckIn.Rehydrate(
            row.CheckInId, row.UserId, recordedAt, row.CreatedAt,
            DatabaseTimestamp, row.Revision + 1, note, row.Measurements);
        return Task.FromResult(true);
    }

    public Task<int> TryUpdateOwnedMeasurementsAsync(
        Guid checkInId,
        IReadOnlyList<Measurement> measurements,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        var index = CheckIns.FindIndex(c => c.CheckInId == checkInId);
        if (index < 0)
        {
            return Task.FromResult(0);
        }

        var row = CheckIns[index];
        var incoming = measurements.ToDictionary(measurement => measurement.DimensionId);
        var next = new List<Measurement>(row.Measurements.Count);
        var affected = 0;

        foreach (var stored in row.Measurements)
        {
            if (incoming.TryGetValue(stored.DimensionId, out var replacement))
            {
                next.Add(Measurement.Rehydrate(stored.DimensionId, stored.DimensionVersionId, replacement.Value));
                affected++;
            }
            else
            {
                next.Add(stored);
            }
        }

        CheckIns[index] = CheckIn.Rehydrate(
            row.CheckInId, row.UserId, row.RecordedAt, row.CreatedAt,
            row.UpdatedAt, row.Revision, row.Note, next);
        return Task.FromResult(affected);
    }

    /// <summary>Simula fallo físico de escritura dentro de la transacción de eliminación.</summary>
    public Exception? DeleteFailure { get; set; }

    public Task<bool> TryDeleteOwnedAsync(
        Guid checkInId,
        Guid ownerId,
        int expectedRevision,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (DeleteFailure is not null)
        {
            throw DeleteFailure;
        }

        // Réplica del predicado SQL de OQ-DOM-009: propiedad + revisión esperada y sin
        // ninguna condición temporal (la eliminación está permitida en cualquier momento).
        var index = CheckIns.FindIndex(c => c.CheckInId == checkInId && c.UserId == ownerId);
        if (index < 0 || CheckIns[index].Revision != expectedRevision)
        {
            return Task.FromResult(false);
        }

        CheckIns.RemoveAt(index);
        return Task.FromResult(true);
    }
}

public sealed class FakeDimensionVersionRepository : IDimensionVersionRepository
{
    public Dictionary<Guid, ActiveDimensionScale> ActiveScales { get; } = new();
    public HashSet<Guid> ExistingDimensions { get; } = new();

    public Task<ActiveDimensionScale?> GetActiveScaleAsync(Guid dimensionId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        ActiveScales.TryGetValue(dimensionId, out var scale);
        return Task.FromResult(scale);
    }

    /// <summary>Escalas congeladas por versión almacenada (independientes del estado ACTIVE).</summary>
    public Dictionary<Guid, StoredDimensionScale> StoredScales { get; } = new();

    public Task<StoredDimensionScale?> GetScaleByVersionIdAsync(Guid dimensionVersionId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        StoredScales.TryGetValue(dimensionVersionId, out var scale);
        return Task.FromResult(scale);
    }

    public Task<bool> DimensionExistsAsync(Guid dimensionId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingDimensions.Contains(dimensionId) || ActiveScales.ContainsKey(dimensionId));
    }
}
