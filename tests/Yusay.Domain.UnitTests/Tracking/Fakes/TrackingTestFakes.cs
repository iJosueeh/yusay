using System.Data.Common;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;

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

    public int ListOwnedCalls { get; private set; }
    public int? LastFetchLimit { get; private set; }
    public DateTimeOffset? LastCursorRecordedAt { get; private set; }
    public Guid? LastCursorCheckInId { get; private set; }

    public Task<IReadOnlyList<CheckIn>> ListOwnedPageAsync(
        Guid ownerId,
        int fetchLimit,
        DateTimeOffset? cursorRecordedAt,
        Guid? cursorCheckInId,
        CancellationToken cancellationToken = default)
    {
        ListOwnedCalls++;
        LastFetchLimit = fetchLimit;
        LastCursorRecordedAt = cursorRecordedAt;
        LastCursorCheckInId = cursorCheckInId;

        IEnumerable<CheckIn> query = CheckIns.Where(c => c.UserId == ownerId);
        if (cursorRecordedAt is not null && cursorCheckInId is not null)
        {
            var recordedAt = cursorRecordedAt.Value;
            var checkInId = cursorCheckInId.Value;
            query = query.Where(c =>
                c.RecordedAt < recordedAt ||
                (c.RecordedAt == recordedAt && c.CheckInId < checkInId));
        }

        var page = query
            .OrderByDescending(c => c.RecordedAt)
            .ThenByDescending(c => c.CheckInId)
            .Take(fetchLimit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<CheckIn>>(page);
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

public sealed class FakeDimensionCatalogRepository : IDimensionCatalogRepository
{
    public List<DimensionWithVersions> Items { get; } = new();

    /// <summary>Invocaciones de las operaciones de lista, por operación.</summary>
    public int ListPublishedCalls { get; private set; }

    public int ListCompleteCalls { get; private set; }

    /// <summary>Dimensiones solicitadas por GetWithHistoryAsync, en orden de invocación.</summary>
    public List<Guid> HistoryCalls { get; } = new();

    public Task<IReadOnlyList<DimensionWithVersions>> ListPublishedAsync(
        CancellationToken cancellationToken = default)
    {
        ListPublishedCalls++;

        var items = Items
            .Select(ApplyPublished)
            .Where(item => item.Versions.Count > 0)
            .ToArray();

        return Task.FromResult<IReadOnlyList<DimensionWithVersions>>(items);
    }

    public Task<DimensionWithVersions?> GetWithHistoryAsync(
        Guid dimensionId,
        CancellationToken cancellationToken = default)
    {
        HistoryCalls.Add(dimensionId);

        var candidate = Items.FirstOrDefault(item => item.Dimension.DimensionId == dimensionId);
        if (candidate is null)
        {
            return Task.FromResult<DimensionWithVersions?>(null);
        }

        var visible = ApplyHistory(candidate);
        return Task.FromResult<DimensionWithVersions?>(visible.Versions.Count > 0 ? visible : null);
    }

    public Task<IReadOnlyList<DimensionWithVersions>> ListCompleteAsync(
        CancellationToken cancellationToken = default)
    {
        ListCompleteCalls++;

        return Task.FromResult<IReadOnlyList<DimensionWithVersions>>(Items.ToArray());
    }

    // Réplica de los predicados de status del repositorio real (B4 de RF-021):
    // publicable solo ACTIVE, histórico ACTIVE/RETIRED y completo sin filtro.
    private static DimensionWithVersions ApplyPublished(DimensionWithVersions item)
    {
        var versions = item.Versions
            .Where(version => version.Status == DimensionVersionStatus.Active)
            .ToArray();

        return versions.Length == item.Versions.Count
            ? item
            : item with { Versions = versions };
    }

    private static DimensionWithVersions ApplyHistory(DimensionWithVersions item)
    {
        var versions = item.Versions
            .Where(version => version.Status is DimensionVersionStatus.Active or DimensionVersionStatus.Retired)
            .ToArray();

        return versions.Length == item.Versions.Count
            ? item
            : item with { Versions = versions };
    }
}

// Doble de IDimensionAdministrationRepository (B5a y B5b de RF-021): réplica del contrato
// del repositorio real —pre-check de code/número de versión, traducción de 23505 acotada,
// lectura para edición que devuelve una copia rehidratada (como el SELECT FOR UPDATE),
// escritura de dimensiones que jamás toca el code y operaciones granulares de anclas— con
// interruptores para simular carreras y fallos físicos. Operations registra la secuencia
// completa de operaciones (lecturas bloqueadas incluidas) para asertar el orden de bloqueos
// y de escritura que exige RF-021.
public sealed class FakeDimensionAdministrationRepository : IDimensionAdministrationRepository
{
    public List<Dimension> Dimensions { get; } = new();
    public HashSet<string> ExistingCodes { get; } = new(StringComparer.Ordinal);

    /// <summary>Versiones almacenadas (reemplazo y lectura como copia FOR UPDATE).</summary>
    public List<DimensionVersion> Versions { get; } = new();

    /// <summary>Secuencia de operaciones observadas, por orden de invocación.</summary>
    public List<string> Operations { get; } = new();

    /// <summary>Invocaciones de la lectura bloqueada de versión, por operación.</summary>
    public int GetVersionForUpdateCalls { get; private set; }

    /// <summary>Simula la traducción de 23505 del repositorio real (número duplicado en la carrera).</summary>
    public bool AddVersionRaceYieldsDuplicateNumber { get; set; }

    /// <summary>Simula un fallo físico de inserción de versión sin traducir (p. ej. PostgreSQL caído).</summary>
    public Exception? AddVersionFailure { get; set; }

    /// <summary>Invocaciones de la lectura bloqueada, por operación.</summary>
    public int GetByIdForUpdateCalls { get; private set; }

    /// <summary>Entidades devueltas por UpdateDetailsAsync, en orden de invocación.</summary>
    public List<Dimension> Updated { get; } = new();

    /// <summary>Simula la traducción de 23505 del repositorio real (code duplicado en la carrera).</summary>
    public bool AddRaceYieldsDuplicateCode { get; set; }

    /// <summary>Simula un fallo físico de inserción sin traducir (p. ej. PostgreSQL caído).</summary>
    public Exception? AddFailure { get; set; }

    public Task<bool> CodeExistsAsync(
        string code,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ExistingCodes.Contains(code));

    public Task AddAsync(
        Dimension dimension,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        if (AddFailure is not null)
        {
            throw AddFailure;
        }

        if (AddRaceYieldsDuplicateCode || ExistingCodes.Contains(dimension.Code.Value))
        {
            throw new ConflictException(IDimensionAdministrationRepository.DuplicateCodeConflictMessage);
        }

        Dimensions.Add(dimension);
        ExistingCodes.Add(dimension.Code.Value);
        return Task.CompletedTask;
    }

    public Task<Dimension?> GetByIdForUpdateAsync(
        Guid dimensionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        GetByIdForUpdateCalls++;
        Operations.Add("GetByIdForUpdate");

        // Copia rehidratada, igual que la lectura FOR UPDATE del repositorio real: la
        // mutación del caso de uso no afecta al almacén hasta UpdateDetailsAsync.
        var stored = Dimensions.Find(dimension => dimension.DimensionId == dimensionId);
        return Task.FromResult(stored is null
            ? null
            : Dimension.Rehydrate(stored.DimensionId, stored.Code, stored.Name, stored.Description));
    }

    public Task UpdateDetailsAsync(
        Dimension dimension,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Updated.Add(dimension);

        var index = Dimensions.FindIndex(stored => stored.DimensionId == dimension.DimensionId);
        if (index >= 0)
        {
            var current = Dimensions[index];
            Dimensions[index] = Dimension.Rehydrate(
                current.DimensionId, current.Code, dimension.Name, dimension.Description);
        }

        return Task.CompletedTask;
    }

    public Task<bool> VersionNumberExistsAsync(
        Guid dimensionId,
        int version,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("VersionNumberExists");
        return Task.FromResult(Versions.Any(
            stored => stored.DimensionId == dimensionId && stored.Version == version));
    }

    public Task AddVersionAsync(
        DimensionVersion version,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("AddVersion");

        if (AddVersionFailure is not null)
        {
            throw AddVersionFailure;
        }

        if (AddVersionRaceYieldsDuplicateNumber || Versions.Any(stored =>
                stored.DimensionId == version.DimensionId && stored.Version == version.Version))
        {
            throw new ConflictException(
                IDimensionAdministrationRepository.DuplicateVersionNumberConflictMessage);
        }

        Versions.Add(version);
        return Task.CompletedTask;
    }

    public Task<DimensionVersion?> GetVersionForUpdateAsync(
        Guid dimensionId,
        Guid dimensionVersionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        GetVersionForUpdateCalls++;
        Operations.Add("GetVersionForUpdate");

        // Copia rehidratada, igual que la lectura FOR UPDATE del repositorio real.
        var stored = Versions.Find(candidate =>
            candidate.DimensionVersionId == dimensionVersionId &&
            candidate.DimensionId == dimensionId);
        return Task.FromResult(stored is null
            ? null
            : Rebuild(stored));
    }

    public Task UpdateVersionDetailsAsync(
        Guid dimensionVersionId,
        string definition,
        int minValue,
        int maxValue,
        int step,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("UpdateVersionDetails");
        ReplaceVersion(dimensionVersionId, stored => Rebuild(
            stored, DimensionScale.Create(minValue, maxValue, step), definition));
        return Task.CompletedTask;
    }

    public Task DeleteAnchorsAsync(
        Guid dimensionVersionId,
        IReadOnlyList<int> values,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("DeleteAnchors");
        ReplaceVersion(dimensionVersionId, stored => Rebuild(
            stored, anchors: stored.Anchors.Where(anchor => !values.Contains(anchor.Value))));
        return Task.CompletedTask;
    }

    public Task UpdateAnchorLabelAsync(
        Guid dimensionVersionId,
        int value,
        string label,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("UpdateAnchorLabel");
        ReplaceVersion(dimensionVersionId, stored => Rebuild(
            stored,
            anchors: stored.Anchors.Select(anchor =>
                anchor.Value == value ? DimensionAnchor.Create(value, label) : anchor)));
        return Task.CompletedTask;
    }

    public Task InsertAnchorsAsync(
        Guid dimensionVersionId,
        IReadOnlyList<DimensionAnchor> anchors,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("InsertAnchors");
        ReplaceVersion(dimensionVersionId, stored => Rebuild(
            stored, anchors: stored.Anchors.Concat(anchors)));
        return Task.CompletedTask;
    }

    public bool ActiveVersionRaceYieldsConflict { get; set; }

    public bool UpdateStatusReturnsNoRows { get; set; }

    public Exception? UpdateStatusFailure { get; set; }

    public Task<Guid?> GetActiveVersionIdAsync(
        Guid dimensionId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("GetActiveVersionId");
        var active = Versions.Find(stored =>
            stored.DimensionId == dimensionId &&
            stored.Status == DimensionVersionStatus.Active);
        return Task.FromResult(active?.DimensionVersionId);
    }

    public Task<bool> UpdateStatusAsync(
        Guid dimensionVersionId,
        DimensionVersionStatus status,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("UpdateStatus");

        if (UpdateStatusFailure is not null)
        {
            throw UpdateStatusFailure;
        }

        if (ActiveVersionRaceYieldsConflict)
        {
            throw new ConflictException(
                IDimensionAdministrationRepository.AnotherActiveVersionConflictMessage);
        }

        if (UpdateStatusReturnsNoRows)
        {
            return Task.FromResult(false);
        }

        ReplaceVersion(dimensionVersionId, stored => Rebuild(stored, status: status));
        return Task.FromResult(true);
    }

    // Réplica de la escritura granular del repositorio real: cada operación rehidrata la
    // versión almacenada con el campo que la sentencia SQL modifica (sin revalidar triggers
    // de escala/anclas, cuya responsabilidad cubren las pruebas de integración).
    private void ReplaceVersion(
        Guid dimensionVersionId,
        Func<DimensionVersion, DimensionVersion> mutate)
    {
        var index = Versions.FindIndex(
            stored => stored.DimensionVersionId == dimensionVersionId);
        if (index >= 0)
        {
            Versions[index] = mutate(Versions[index]);
        }
    }

    private static DimensionVersion Rebuild(
        DimensionVersion version,
        DimensionScale? scale = null,
        string? definition = null,
        IEnumerable<DimensionAnchor>? anchors = null,
        DimensionVersionStatus? status = null) =>
        DimensionVersion.Rehydrate(
            version.DimensionVersionId,
            version.DimensionId,
            version.Version,
            scale ?? version.Scale,
            definition ?? version.Definition,
            status ?? version.Status,
            anchors ?? version.Anchors);
}
