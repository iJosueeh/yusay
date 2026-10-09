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

    public Task<bool> DimensionExistsAsync(Guid dimensionId, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(ExistingDimensions.Contains(dimensionId) || ActiveScales.ContainsKey(dimensionId));
    }
}
