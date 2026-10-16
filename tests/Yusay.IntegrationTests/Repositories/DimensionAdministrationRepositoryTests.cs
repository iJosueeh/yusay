using System.Diagnostics;
using System.Text;
using Dapper;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

/// <summary>
/// Pruebas de integración del repositorio de administración (B5a de RF-021): alta con
/// traducción acotada de 23505 (uq_dimension_code → ConflictException con el mensaje del
/// contrato), lectura FOR UPDATE que serializa la edición bajo bloqueo de fila (última
/// escritura gana y el segundo lector observa el estado commiteado), escritura que jamás
/// toca el code inmutable y rollback de la transacción del UnitOfWork cuando la operación
/// posterior al alta falla (p. ej. la auditoría). Los datos se siembran por prueba con
/// códigos únicos porque la base de la colección es compartida.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class DimensionAdministrationRepositoryTests
{
    private static readonly TimeSpan BlockingWait = TimeSpan.FromSeconds(15);

    private readonly PostgreSqlFixture _fixture;
    private readonly DimensionAdministrationRepository _repository;
    private readonly UnitOfWork _unitOfWork;

    public DimensionAdministrationRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _repository = new DimensionAdministrationRepository(fixture.ConnectionFactory);
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
    }

    [Fact]
    public async Task AddAsync_ShouldPersistTheDimensionRow()
    {
        var dimension = CreateDimension("persist");

        await _repository.AddAsync(dimension);

        var row = await QueryByCodeAsync(dimension.Code.Value);
        Assert.NotNull(row);
        Assert.Equal(dimension.DimensionId, row!.Dimension_id);
        Assert.Equal("Nombre de prueba", row.Name);
        Assert.Equal("Descripción de prueba.", row.Description);
    }

    [Fact]
    public async Task CodeExistsAsync_ShouldReflectPersistedCodes()
    {
        var dimension = CreateDimension("exists");

        Assert.False(await _repository.CodeExistsAsync(dimension.Code.Value));

        await _repository.AddAsync(dimension);

        Assert.True(await _repository.CodeExistsAsync(dimension.Code.Value));
        Assert.False(await _repository.CodeExistsAsync($"{dimension.Code.Value}_other"));
    }

    [Fact]
    public async Task AddAsync_WithDuplicatedCode_ShouldTranslateToConflictWithTheContractMessage()
    {
        var original = CreateDimension("dupe");
        await _repository.AddAsync(original);

        // El segundo registro reutiliza el code almacenado: la violación de uq_dimension_code
        // debe traducirse con el mismo mensaje del pre-check del caso de uso
        var duplicate = Dimension.Create(
            DimensionCode.Create(original.Code.Value), "Otra dimensión", "Código duplicado.");

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => _repository.AddAsync(duplicate));

        Assert.Equal(IDimensionAdministrationRepository.DuplicateCodeConflictMessage, exception.Message);
        Assert.Equal(1, await CountByCodeAsync(duplicate.Code.Value));
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ShouldRehydrateAndReturnNullForUnknown()
    {
        var dimension = CreateDimension("rehydrate");
        await _repository.AddAsync(dimension);

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var loaded = await _repository.GetByIdForUpdateAsync(dimension.DimensionId, transaction);
        var unknown = await _repository.GetByIdForUpdateAsync(Guid.NewGuid(), transaction);
        await transaction.RollbackAsync();

        Assert.NotNull(loaded);
        Assert.Equal(dimension.DimensionId, loaded!.DimensionId);
        Assert.Equal(dimension.Code.Value, loaded.Code.Value);
        Assert.Equal("Nombre de prueba", loaded.Name);
        Assert.Null(unknown);
    }

    [Fact]
    public async Task GetByIdForUpdateAsync_ShouldBlockConcurrentReadersUntilCommit()
    {
        var dimensionId = await SeedDimensionAsync("forupdate");

        await using var holder = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var holderTransaction = await holder.BeginTransactionAsync();
        var held = await _repository.GetByIdForUpdateAsync(dimensionId, holderTransaction);
        Assert.NotNull(held);

        // El segundo lector captura su pid antes del FOR UPDATE y queda bloqueado por el
        // bloqueo de fila del primer lector hasta que este commitee.
        var readerStarted = new TaskCompletionSource<int>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var readerTask = Task.Run(async () =>
        {
            await using var reader = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await using var readerTransaction = await reader.BeginTransactionAsync();
            var pid = await reader.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT pg_backend_pid();", transaction: readerTransaction));
            readerStarted.SetResult(pid);

            return await _repository.GetByIdForUpdateAsync(dimensionId, readerTransaction);
        });

        var pid = await readerStarted.Task.WaitAsync(BlockingWait);
        Assert.True(
            await WaitForBlockingAsync(pid),
            "La segunda lectura FOR UPDATE debería bloquearse tras la primera.");

        // Bajo el bloqueo, el titular escribe y commitea: el lector liberado observa ese estado
        await holder.ExecuteAsync(new CommandDefinition(
            "UPDATE yusay.dimension SET name = 'Nombre commiteado' WHERE dimension_id = @Id",
            new { Id = dimensionId }, holderTransaction));
        await holderTransaction.CommitAsync();

        var observed = await readerTask.WaitAsync(BlockingWait);
        Assert.Equal("Nombre commiteado", observed!.Name);
    }

    [Fact]
    public async Task UpdateDetailsAsync_ShouldPersistNameAndDescriptionWithoutTouchingCode()
    {
        var dimension = CreateDimension("immutable_code");
        await _repository.AddAsync(dimension);

        dimension.UpdateDetails("Nombre revisado", "Descripción revisada.");

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await _repository.UpdateDetailsAsync(dimension, transaction);
        await transaction.CommitAsync();

        var row = await QueryByCodeAsync(dimension.Code.Value);
        Assert.NotNull(row);
        Assert.Equal("Nombre revisado", row!.Name);
        Assert.Equal("Descripción revisada.", row.Description);
        Assert.Equal(dimension.Code.Value, row.Code);
    }

    [Fact]
    public async Task UnitOfWork_WhenTheOperationFailsAfterAddAsync_ShouldRollBackTheInsert()
    {
        var dimension = CreateDimension("rollback");

        // Réplica del camino del caso de uso: alta y operación posterior (auditoría) en la
        // misma transacción; el fallo posterior debe revertir el alta completa.
        await Assert.ThrowsAsync<InvalidOperationException>(() => _unitOfWork.ExecuteInTransactionAsync(
            async tx =>
            {
                await _repository.AddAsync(dimension, tx);
                throw new InvalidOperationException("Fallo simulado de la auditoría en la transacción.");
            }));

        Assert.Equal(0, await CountByCodeAsync(dimension.Code.Value));
    }

    // ------------------------------------------------------------------------------------------
    // Semillas y utilidades
    // ------------------------------------------------------------------------------------------

    private static Dimension CreateDimension(string label) => Dimension.Create(
        DimensionCode.Create($"b5ar_{label}_{Guid.NewGuid():N}"),
        "Nombre de prueba",
        "Descripción de prueba.");

    private async Task<Guid> SeedDimensionAsync(string label)
    {
        var dimensionId = Guid.NewGuid();
        var code = $"b5ar_{label}_{Guid.NewGuid():N}";

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, @Code, 'Nombre de prueba', 'Descripción de prueba.');
            """, new { DimensionId = dimensionId, Code = code }));

        return dimensionId;
    }

    private sealed class DimensionCodeRow
    {
        public Guid Dimension_id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    private async Task<DimensionCodeRow?> QueryByCodeAsync(string code)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<DimensionCodeRow>(new CommandDefinition(
            "SELECT dimension_id, code, name, description FROM yusay.dimension WHERE code = @Code",
            new { Code = code }));
    }

    private async Task<long> CountByCodeAsync(string code)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension WHERE code = @Code", new { Code = code }));
    }

    /// <summary>
    /// Espera acotada a que la sesión indicada quede bloqueada (wait_event_type 'Lock'),
    /// igual que el precedente de DimensionScaleIntegrityTests.
    /// </summary>
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
