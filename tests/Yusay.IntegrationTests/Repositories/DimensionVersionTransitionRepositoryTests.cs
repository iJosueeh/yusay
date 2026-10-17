using System.Diagnostics;
using Dapper;
using Npgsql;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

[Collection("DatabaseCollection")]
public sealed class DimensionVersionTransitionRepositoryTests
{
    private static readonly TimeSpan BlockingWait = TimeSpan.FromSeconds(15);

    private readonly PostgreSqlFixture _fixture;
    private readonly DimensionAdministrationRepository _repository;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepository;

    public DimensionVersionTransitionRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _repository = new DimensionAdministrationRepository(fixture.ConnectionFactory);
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepository = new UserAccountRepository(fixture.ConnectionFactory);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldPersistTheLegalDraftActiveRetiredTransitions()
    {
        var dimensionId = await SeedDimensionAsync("transitions");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await UpdateStatusAsync(version.DimensionVersionId, DimensionVersionStatus.Active);
        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(version.DimensionVersionId))!.Status);

        await UpdateStatusAsync(version.DimensionVersionId, DimensionVersionStatus.Retired);
        Assert.Equal("RETIRED", (await QueryVersionRowAsync(version.DimensionVersionId))!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ForDirectDraftToRetiredTransition_ShouldPropagateTheV015ViolationUntranslated()
    {
        var dimensionId = await SeedDimensionAsync("v015");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await _repository.UpdateStatusAsync(
                version.DimensionVersionId, DimensionVersionStatus.Retired, transaction);
        });

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Contains("Transición de estado inválida", exception.Message, StringComparison.Ordinal);
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(version.DimensionVersionId))!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenAnotherVersionIsAlreadyActive_ShouldTranslateTheUniqueIndexToTheContractConflict()
    {
        var dimensionId = await SeedDimensionAsync("singleactive");
        var first = await AddVersionAsync(CreateVersion(dimensionId, 1));
        var second = await AddVersionAsync(CreateVersion(dimensionId, 2));
        await UpdateStatusAsync(first.DimensionVersionId, DimensionVersionStatus.Active);

        var exception = await Assert.ThrowsAsync<ConflictException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await _repository.UpdateStatusAsync(
                second.DimensionVersionId, DimensionVersionStatus.Active, transaction);
        });

        Assert.Equal(
            IDimensionAdministrationRepository.AnotherActiveVersionConflictMessage,
            exception.Message);
        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(first.DimensionVersionId))!.Status);
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(second.DimensionVersionId))!.Status);
    }

    [Fact]
    public async Task GetActiveVersionIdAsync_ShouldReturnTheActiveVersionScopedByDimensionOrNull()
    {
        var dimensionId = await SeedDimensionAsync("activeid");
        var otherDimensionId = await SeedDimensionAsync("activeid_other");
        var draft = await AddVersionAsync(CreateVersion(dimensionId, 1));
        var active = await AddVersionAsync(CreateVersion(dimensionId, 2));
        var otherActive = await AddVersionAsync(CreateVersion(otherDimensionId, 1));
        await UpdateStatusAsync(active.DimensionVersionId, DimensionVersionStatus.Active);
        await UpdateStatusAsync(otherActive.DimensionVersionId, DimensionVersionStatus.Active);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            Assert.Equal(active.DimensionVersionId, await _repository.GetActiveVersionIdAsync(
                dimensionId, transaction));
            Assert.Equal(otherActive.DimensionVersionId, await _repository.GetActiveVersionIdAsync(
                otherDimensionId, transaction));
            await transaction.RollbackAsync();
        }

        await UpdateStatusAsync(active.DimensionVersionId, DimensionVersionStatus.Retired);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            Assert.Null(await _repository.GetActiveVersionIdAsync(dimensionId, transaction));
            Assert.Equal(otherActive.DimensionVersionId, await _repository.GetActiveVersionIdAsync(
                otherDimensionId, transaction));
            await transaction.RollbackAsync();
        }

        Assert.Equal("RETIRED", (await QueryVersionRowAsync(active.DimensionVersionId))!.Status);
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(draft.DimensionVersionId))!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_OnActivationWithAnAnchorOutsideTheScale_ShouldPropagateTheG2ViolationUntranslated()
    {
        var dimensionId = await SeedDimensionAsync("g2");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));
        await InsertCorruptedAnchorAsync(version.DimensionVersionId, 7, "Muy alto");

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await _repository.UpdateStatusAsync(
                version.DimensionVersionId, DimensionVersionStatus.Active, transaction);
        });

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Contains("No se puede activar la versión", exception.Message, StringComparison.Ordinal);
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(version.DimensionVersionId))!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_ShouldWaitForTheVersionRowLockHeldByAnotherTransaction()
    {
        var dimensionId = await SeedDimensionAsync("rowlock");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await using var holder = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var holderTransaction = await holder.BeginTransactionAsync();
        var held = await _repository.GetVersionForUpdateAsync(
            dimensionId, version.DimensionVersionId, holderTransaction);
        Assert.NotNull(held);

        var writerStarted = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var writerTask = Task.Run(async () =>
        {
            await using var writer = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var writerTransaction = await writer.BeginTransactionAsync();
            var pid = await writer.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT pg_backend_pid();", transaction: writerTransaction));
            writerStarted.SetResult(pid);

            var updated = await _repository.UpdateStatusAsync(
                version.DimensionVersionId, DimensionVersionStatus.Active, writerTransaction);
            await writerTransaction.CommitAsync();
            return updated;
        });

        var pid = await writerStarted.Task.WaitAsync(BlockingWait);
        Assert.True(
            await WaitForBlockingAsync(pid),
            "La escritura de estado debería bloquearse tras el bloqueo FOR UPDATE de la versión.");

        await holderTransaction.CommitAsync();

        Assert.True(await writerTask.WaitAsync(BlockingWait));
        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(version.DimensionVersionId))!.Status);
    }

    [Fact]
    public async Task UnitOfWork_WhenTheOperationFailsAfterUpdateStatusAsync_ShouldRollBackTheStatus()
    {
        var dimensionId = await SeedDimensionAsync("rollback_status");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteInTransactionAsync(
            async tx =>
            {
                var updated = await _repository.UpdateStatusAsync(
                    version.DimensionVersionId, DimensionVersionStatus.Active, tx);
                Assert.True(updated);
                throw new InvalidOperationException("Fallo simulado de la auditoría en la transacción.");
            }));

        Assert.Equal("DRAFT", (await QueryVersionRowAsync(version.DimensionVersionId))!.Status);
    }

    [Fact]
    public async Task UpdateStatusAsync_RetiringAVersionWithHistoricalMeasurements_ShouldPreserveThemAndRestrictDeletion()
    {
        var dimensionId = await SeedDimensionAsync("history");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));
        await UpdateStatusAsync(version.DimensionVersionId, DimensionVersionStatus.Active);

        var checkInId = Guid.NewGuid();
        var userId = await CreateTestUserAsync();
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at)
                VALUES (@CheckInId, @UserId, clock_timestamp(), clock_timestamp());
                INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
                VALUES (@CheckInId, @DimensionId, @VersionId, 5);
                """,
                new { CheckInId = checkInId, UserId = userId, DimensionId = dimensionId,
                    VersionId = version.DimensionVersionId }));
        }

        await UpdateStatusAsync(version.DimensionVersionId, DimensionVersionStatus.Retired);

        Assert.Equal(1, await CountMeasurementsAsync(version.DimensionVersionId));

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM yusay.dimension_version WHERE dimension_version_id = @VersionId",
                new { VersionId = version.DimensionVersionId }));
        });

        Assert.Equal("23001", exception.SqlState);
        Assert.True(
            exception.ConstraintName is "fk_measurement_dimension_version"
                or "fk_dimension_anchor_dimension_version",
            $"La eliminación debería haber sido restringida por una FK, no por '{exception.ConstraintName}'.");
        Assert.Equal("RETIRED", (await QueryVersionRowAsync(version.DimensionVersionId))!.Status);
        Assert.Equal(1, await CountMeasurementsAsync(version.DimensionVersionId));
    }

    private async Task UpdateStatusAsync(Guid dimensionVersionId, DimensionVersionStatus status)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var updated = await _repository.UpdateStatusAsync(dimensionVersionId, status, transaction);
        Assert.True(updated);
        await transaction.CommitAsync();
    }

    private async Task InsertCorruptedAnchorAsync(Guid dimensionVersionId, int value, string label)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "ALTER TABLE yusay.dimension_anchor DISABLE TRIGGER trg_guard_dimension_anchor_scale;");
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label)
                VALUES (@VersionId, @Value, @Label);
                """,
                new { VersionId = dimensionVersionId, Value = value, Label = label }));
        }
        finally
        {
            await connection.ExecuteAsync(
                "ALTER TABLE yusay.dimension_anchor ENABLE TRIGGER trg_guard_dimension_anchor_scale;");
        }
    }

    private async Task<Guid> CreateTestUserAsync()
    {
        var user = UserAccount.Create(
            Email.Create($"b6_repo_{Guid.NewGuid():N}@yusay.local"),
            DateTimeOffset.UtcNow.AddYears(-25));
        await _userAccountRepository.AddAsync(user);
        return user.Id;
    }

    private static DimensionVersion CreateVersion(Guid dimensionId, int version) =>
        DimensionVersion.CreateDraft(
            dimensionId,
            version,
            DimensionScale.Create(1, 5, 1),
            "Percepción de ánimo del día.",
            [
                DimensionAnchor.Create(1, "Bajo"), DimensionAnchor.Create(3, "Medio"),
                DimensionAnchor.Create(5, "Alto")
            ]);

    private async Task<DimensionVersion> AddVersionAsync(DimensionVersion version)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await _repository.AddVersionAsync(version, transaction);
        await transaction.CommitAsync();
        return version;
    }

    private async Task<Guid> SeedDimensionAsync(string label)
    {
        var dimensionId = Guid.NewGuid();
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, @Code, 'Nombre de prueba', 'Descripción de prueba.');
            """,
            new { DimensionId = dimensionId, Code = $"b6tv_{label}_{Guid.NewGuid():N}" }));
        return dimensionId;
    }

    private sealed class VersionRow
    {
        public string Status { get; init; } = string.Empty;
    }

    private async Task<VersionRow?> QueryVersionRowAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition(
            """
            SELECT status
            FROM yusay.dimension_version
            WHERE dimension_version_id = @DimensionVersionId
            """,
            new { DimensionVersionId = dimensionVersionId }));
    }

    private async Task<int> CountMeasurementsAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.measurement WHERE dimension_version_id = @VersionId",
            new { VersionId = dimensionVersionId }));
    }

    private async Task<bool> WaitForBlockingAsync(int pid)
    {
        var stopwatch = Stopwatch.StartNew();

        await using var monitor = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        while (stopwatch.Elapsed < BlockingWait)
        {
            var waiting = await monitor.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT count(*)::int FROM pg_stat_activity WHERE pid = @Pid AND wait_event_type = 'Lock'",
                new { Pid = pid }));
            if (waiting > 0)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }
}
