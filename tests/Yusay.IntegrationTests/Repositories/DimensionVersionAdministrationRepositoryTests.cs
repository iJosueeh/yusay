using System.Diagnostics;
using Dapper;
using Npgsql;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

[Collection("DatabaseCollection")]
public sealed class DimensionVersionAdministrationRepositoryTests
{
    private static readonly TimeSpan BlockingWait = TimeSpan.FromSeconds(15);

    private readonly PostgreSqlFixture _fixture;
    private readonly DimensionAdministrationRepository _repository;
    private readonly UnitOfWork _unitOfWork;

    public DimensionVersionAdministrationRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _repository = new DimensionAdministrationRepository(fixture.ConnectionFactory);
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
    }

    [Fact]
    public async Task AddVersionAsync_ShouldPersistTheVersionRowAndItsAnchors()
    {
        var dimensionId = await SeedDimensionAsync("persist");
        var version = CreateVersion(dimensionId, 1);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repository.AddVersionAsync(version, transaction);
            await transaction.CommitAsync();
        }

        var row = await QueryVersionRowAsync(version.DimensionVersionId);
        Assert.NotNull(row);
        Assert.Equal(dimensionId, row!.Dimension_id);
        Assert.Equal(1, row.Version);
        Assert.Equal("Percepción de ánimo del día.", row.Definition);
        Assert.Equal(1, row.Min_value);
        Assert.Equal(5, row.Max_value);
        Assert.Equal(1, row.Step);
        Assert.Equal("DRAFT", row.Status);

        Assert.Equal(
            [(1, "Bajo"), (3, "Medio"), (5, "Alto")],
            await QueryAnchorsAsync(version.DimensionVersionId));
    }

    [Fact]
    public async Task AddVersionAsync_WithoutAnchors_ShouldPersistOnlyTheVersionRow()
    {
        var dimensionId = await SeedDimensionAsync("noanchors");
        var version = CreateVersion(dimensionId, 1, withAnchors: false);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repository.AddVersionAsync(version, transaction);
            await transaction.CommitAsync();
        }

        Assert.NotNull(await QueryVersionRowAsync(version.DimensionVersionId));
        Assert.Empty(await QueryAnchorsAsync(version.DimensionVersionId));
    }

    [Fact]
    public async Task AddVersionAsync_WithDuplicatedVersionNumber_ShouldTranslateToConflictWithTheContractMessage()
    {
        var dimensionId = await SeedDimensionAsync("dupe");
        await AddVersionAsync(CreateVersion(dimensionId, 2));

        var duplicate = CreateVersion(dimensionId, 2);

        var exception = await Assert.ThrowsAsync<ConflictException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await _repository.AddVersionAsync(duplicate, transaction);
        });

        Assert.Equal(
            IDimensionAdministrationRepository.DuplicateVersionNumberConflictMessage,
            exception.Message);
        Assert.Equal(1, await CountVersionsAsync(dimensionId));
        Assert.Equal(0, await CountAnchorsAsync(duplicate.DimensionVersionId));
    }

    [Fact]
    public async Task VersionNumberExistsAsync_ShouldReflectPersistedNumbersScopedByDimension()
    {
        var dimensionId = await SeedDimensionAsync("exists");
        var otherDimensionId = await SeedDimensionAsync("other");
        await AddVersionAsync(CreateVersion(dimensionId, 1));

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        Assert.True(await _repository.VersionNumberExistsAsync(dimensionId, 1, transaction));
        Assert.False(await _repository.VersionNumberExistsAsync(dimensionId, 2, transaction));
        Assert.False(await _repository.VersionNumberExistsAsync(otherDimensionId, 1, transaction));
        await transaction.RollbackAsync();
    }

    [Fact]
    public async Task GetVersionForUpdateAsync_ShouldRehydrateWithAnchorsAndReturnNullForUnknownOrForeign()
    {
        var dimensionId = await SeedDimensionAsync("rehydrate");
        var otherDimensionId = await SeedDimensionAsync("foreign");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var loaded = await _repository.GetVersionForUpdateAsync(
            dimensionId, version.DimensionVersionId, transaction);
        var unknown = await _repository.GetVersionForUpdateAsync(
            dimensionId, Guid.NewGuid(), transaction);
        var foreign = await _repository.GetVersionForUpdateAsync(
            otherDimensionId, version.DimensionVersionId, transaction);
        await transaction.RollbackAsync();

        Assert.NotNull(loaded);
        Assert.Equal(version.DimensionVersionId, loaded!.DimensionVersionId);
        Assert.Equal(dimensionId, loaded.DimensionId);
        Assert.Equal("Percepción de ánimo del día.", loaded.Definition);
        Assert.Equal([(1, "Bajo"), (3, "Medio"), (5, "Alto")],
            loaded.Anchors.Select(anchor => (anchor.Value, anchor.Label)));
        Assert.Null(unknown);
        Assert.Null(foreign);
    }

    [Fact]
    public async Task GetVersionForUpdateAsync_ShouldBlockConcurrentReadersUntilCommit()
    {
        var dimensionId = await SeedDimensionAsync("forupdate");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await using var holder = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var holderTransaction = await holder.BeginTransactionAsync();
        var held = await _repository.GetVersionForUpdateAsync(
            dimensionId, version.DimensionVersionId, holderTransaction);
        Assert.NotNull(held);

        var readerStarted = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var readerTask = Task.Run(async () =>
        {
            await using var reader = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var readerTransaction = await reader.BeginTransactionAsync();
            var pid = await reader.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT pg_backend_pid();", transaction: readerTransaction));
            readerStarted.SetResult(pid);

            return await _repository.GetVersionForUpdateAsync(
                dimensionId, version.DimensionVersionId, readerTransaction);
        });

        var pid = await readerStarted.Task.WaitAsync(BlockingWait);
        Assert.True(
            await WaitForBlockingAsync(pid),
            "La segunda lectura FOR UPDATE de la versión debería bloquearse tras la primera.");

        await holder.ExecuteAsync(new CommandDefinition(
            "UPDATE yusay.dimension_version SET definition = 'Definición commiteada.' " +
            "WHERE dimension_version_id = @Id",
            new { Id = version.DimensionVersionId }, holderTransaction));
        await holderTransaction.CommitAsync();

        var observed = await readerTask.WaitAsync(BlockingWait);
        Assert.Equal("Definición commiteada.", observed!.Definition);
    }

    [Fact]
    public async Task UpdateVersionDetailsAsync_ShouldPersistDefinitionAndScale()
    {
        var dimensionId = await SeedDimensionAsync("details");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repository.UpdateVersionDetailsAsync(
                version.DimensionVersionId, "Definición revisada.", 1, 7, 1, transaction);
            await transaction.CommitAsync();
        }

        var row = await QueryVersionRowAsync(version.DimensionVersionId);
        Assert.Equal("Definición revisada.", row!.Definition);
        Assert.Equal(7, row.Max_value);
    }

    [Fact]
    public async Task GranularAnchorOperations_ShouldPersistDeletesRelabelsAndInserts()
    {
        var dimensionId = await SeedDimensionAsync("granular");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repository.DeleteAnchorsAsync(
                version.DimensionVersionId, [3, 5], transaction);
            await _repository.UpdateAnchorLabelAsync(
                version.DimensionVersionId, 1, "Muy bajo", transaction);
            await _repository.InsertAnchorsAsync(
                version.DimensionVersionId,
                [DimensionAnchor.Create(2, "Bajo-medio"), DimensionAnchor.Create(4, "Alto")],
                transaction);
            await transaction.CommitAsync();
        }

        Assert.Equal([(1, "Muy bajo"), (2, "Bajo-medio"), (4, "Alto")],
            await QueryAnchorsAsync(version.DimensionVersionId));
    }

    [Fact]
    public async Task OrderedEdits_ShouldShrinkTheScaleAfterRemovingTheIncompatibleAnchors()
    {
        var dimensionId = await SeedDimensionAsync("g4order");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await _repository.DeleteAnchorsAsync(
                version.DimensionVersionId, [3, 5], transaction);
            await _repository.UpdateVersionDetailsAsync(
                version.DimensionVersionId, "Percepción de ánimo del día.", 1, 3, 1, transaction);
            await _repository.InsertAnchorsAsync(
                version.DimensionVersionId, [DimensionAnchor.Create(2, "Medio")], transaction);
            await transaction.CommitAsync();
        }

        var row = await QueryVersionRowAsync(version.DimensionVersionId);
        Assert.Equal(3, row!.Max_value);
        Assert.Equal([(1, "Bajo"), (2, "Medio")],
            await QueryAnchorsAsync(version.DimensionVersionId));
    }

    [Fact]
    public async Task UpdateVersionDetailsAsync_WithIncompatibleStoredAnchors_ShouldPropagateTheG4ViolationUntranslated()
    {
        var dimensionId = await SeedDimensionAsync("g4");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await _repository.UpdateVersionDetailsAsync(
                version.DimensionVersionId, "Percepción de ánimo del día.", 1, 3, 1, transaction);
        });

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task InsertAnchorsAsync_OutsideTheScale_ShouldPropagateTheG1ViolationUntranslated()
    {
        var dimensionId = await SeedDimensionAsync("g1");
        var version = await AddVersionAsync(CreateVersion(dimensionId, 1));

        var exception = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            await _repository.InsertAnchorsAsync(
                version.DimensionVersionId, [DimensionAnchor.Create(7, "Muy alto")], transaction);
        });

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
    }

    [Fact]
    public async Task UnitOfWork_WhenTheOperationFailsAfterAddVersionAsync_ShouldRollBackTheVersion()
    {
        var dimensionId = await SeedDimensionAsync("rollback");
        var version = CreateVersion(dimensionId, 1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteInTransactionAsync(
            async tx =>
            {
                await _repository.AddVersionAsync(version, tx);
                throw new InvalidOperationException("Fallo simulado de la auditoría en la transacción.");
            }));

        Assert.Equal(0, await CountVersionsAsync(dimensionId));
        Assert.Equal(0, await CountAnchorsAsync(version.DimensionVersionId));
    }

    private static DimensionVersion CreateVersion(
        Guid dimensionId, int version, bool withAnchors = true) =>
        DimensionVersion.CreateDraft(
            dimensionId,
            version,
            DimensionScale.Create(1, 5, 1),
            "Percepción de ánimo del día.",
            withAnchors
                ? new[]
                {
                    DimensionAnchor.Create(1, "Bajo"), DimensionAnchor.Create(3, "Medio"),
                    DimensionAnchor.Create(5, "Alto")
                }
                : null);

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
            new { DimensionId = dimensionId, Code = $"b5bv_{label}_{Guid.NewGuid():N}" }));
        return dimensionId;
    }

    private sealed class VersionRow
    {
        public Guid Dimension_id { get; init; }
        public int Version { get; init; }
        public string Definition { get; init; } = string.Empty;
        public int Min_value { get; init; }
        public int Max_value { get; init; }
        public int Step { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    private async Task<VersionRow?> QueryVersionRowAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition(
            """
            SELECT dimension_id, version, definition, min_value, max_value, step, status
            FROM yusay.dimension_version
            WHERE dimension_version_id = @DimensionVersionId
            """,
            new { DimensionVersionId = dimensionVersionId }));
    }

    private async Task<(int Value, string Label)[]> QueryAnchorsAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var rows = await connection.QueryAsync<(int Value, string Label)>(new CommandDefinition(
            """
            SELECT value, label
            FROM yusay.dimension_anchor
            WHERE dimension_version_id = @DimensionVersionId
            ORDER BY value
            """,
            new { DimensionVersionId = dimensionVersionId }));
        return rows.ToArray();
    }

    private async Task<long> CountVersionsAsync(Guid dimensionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension_version WHERE dimension_id = @DimensionId",
            new { DimensionId = dimensionId }));
    }

    private async Task<long> CountAnchorsAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension_anchor WHERE dimension_version_id = @DimensionVersionId",
            new { DimensionVersionId = dimensionVersionId }));
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
