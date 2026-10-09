using Dapper;
using Npgsql;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.Tracking.Entities;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

[Collection("DatabaseCollection")]
public sealed class CheckInRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly CheckInRepository _repository;
    private readonly DimensionVersionRepository _dimensionVersionRepository;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepository;

    public CheckInRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _repository = new CheckInRepository(fixture.ConnectionFactory);
        _dimensionVersionRepository = new DimensionVersionRepository(fixture.ConnectionFactory);
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepository = new UserAccountRepository(fixture.ConnectionFactory);
    }

    private async Task<Guid> CreateTestUserAsync()
    {
        var user = UserAccount.Create(
            Email.Create($"checkin_repo_{Guid.NewGuid():N}@yusay.local"),
            DateTimeOffset.UtcNow.AddYears(-25));
        await _userAccountRepository.AddAsync(user);
        return user.Id;
    }

    private async Task<(Guid DimensionId, Guid ActiveVersionId)> SeedDimensionAsync(
        int minValue = 1, int maxValue = 5, int step = 1, bool withRetiredVersion = false)
    {
        var dimensionId = Guid.NewGuid();
        var activeVersionId = Guid.NewGuid();

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, 'dim_' || replace(@DimensionId::text, '-', ''), 'Dimensión de prueba', 'Escala de prueba.');
            """, new { DimensionId = dimensionId }));

        if (withRetiredVersion)
        {
            // Versión retirada previa: la resolución ACTIVE no debe devolverla nunca
            await connection.ExecuteAsync(new CommandDefinition($"""
                INSERT INTO yusay.dimension_version
                    (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
                VALUES (@VersionId, @DimensionId, 1, 'Versión retirada.', @MinValue, @MaxValue, @Step, 'RETIRED');
                """, new { VersionId = Guid.NewGuid(), DimensionId = dimensionId, MinValue = minValue, MaxValue = maxValue, Step = step }));
        }

        await connection.ExecuteAsync(new CommandDefinition($"""
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, @Version, 'Versión activa de prueba.', @MinValue, @MaxValue, @Step, 'ACTIVE');
            """, new
            {
                VersionId = activeVersionId,
                DimensionId = dimensionId,
                Version = withRetiredVersion ? 2 : 1,
                MinValue = minValue,
                MaxValue = maxValue,
                Step = step
            }));

        return (dimensionId, activeVersionId);
    }

    private static CheckIn BuildCheckIn(Guid userId, params Measurement[] measurements) =>
        CheckIn.Create(userId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, measurements);

    [Fact]
    public async Task CreateAsync_And_GetByIdOwnedAsync_ShouldRoundTripWithServerDefaults()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, Measurement.Create(dimensionId, versionId, 4));

        await _repository.CreateAsync(checkIn);
        var retrieved = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);

        Assert.NotNull(retrieved);
        Assert.Equal(checkIn.CheckInId, retrieved.CheckInId);
        Assert.Equal(userId, retrieved.UserId);
        Assert.Equal(1, retrieved.Revision);
        Assert.Null(retrieved.UpdatedAt);
        Assert.Null(retrieved.Note);
        var measurement = Assert.Single(retrieved.Measurements);
        Assert.Equal(dimensionId, measurement.DimensionId);
        Assert.Equal(versionId, measurement.DimensionVersionId);
        Assert.Equal(4, measurement.Value);
    }

    [Fact]
    public async Task CreateAsync_WithNote_ShouldPersistNote()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = CheckIn.Create(
            userId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Nota de prueba",
            [Measurement.Create(dimensionId, versionId, 2)]);

        await _repository.CreateAsync(checkIn);
        var retrieved = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);

        Assert.Equal("Nota de prueba", retrieved!.Note);
    }

    [Fact]
    public async Task GetByIdOwnedAsync_ShouldReturnNullIndistinguishablyForForeignAndNonexistent()
    {
        var ownerId = await CreateTestUserAsync();
        var otherUserId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(ownerId, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var owned = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, ownerId);
        var foreign = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, otherUserId);
        var nonexistent = await _repository.GetByIdOwnedAsync(Guid.NewGuid(), ownerId);

        Assert.NotNull(owned);
        Assert.Null(foreign);
        Assert.Null(nonexistent);
    }

    [Fact]
    public async Task DirectInsert_WithDuplicateDimensionMeasurement_ShouldViolatePrimaryKey()
    {
        // Red física de RN-030: la PK (check_in_id, dimension_id) impide mediciones duplicadas
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
                VALUES (@CheckInId, @DimensionId, @VersionId, 5);
                """, new { CheckInId = checkIn.CheckInId, DimensionId = dimensionId, VersionId = versionId })));

        Assert.Equal("23505", ex.SqlState);
    }

    [Fact]
    public async Task DirectInsert_WithRecordedAtOutsideWindow_ShouldViolateCheckConstraint()
    {
        var userId = await CreateTestUserAsync();

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at)
                VALUES (@Id, @UserId, clock_timestamp() - interval '200 hours', clock_timestamp());
                """, new { Id = Guid.NewGuid(), UserId = userId })));

        Assert.Equal("23514", ex.SqlState);
        Assert.Contains("ck_check_in_recorded_window", ex.ConstraintName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetActiveScaleAsync_ShouldResolveOnlyTheActiveVersion()
    {
        var (dimensionId, activeVersionId) = await SeedDimensionAsync(1, 9, 2, withRetiredVersion: true);

        var scale = await _dimensionVersionRepository.GetActiveScaleAsync(dimensionId);

        Assert.NotNull(scale);
        Assert.Equal(activeVersionId, scale!.DimensionVersionId);
        Assert.Equal(1, scale.MinValue);
        Assert.Equal(9, scale.MaxValue);
        Assert.Equal(2, scale.Step);
    }

    [Fact]
    public async Task GetActiveScaleAsync_WithOnlyRetiredVersion_ShouldReturnNull()
    {
        var (dimensionId, _) = await SeedDimensionAsync(withRetiredVersion: false);

        // Retirar la única versión activa: transición ACTIVE → RETIRED (permitida por V015)
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE yusay.dimension_version SET status = 'RETIRED'
                WHERE dimension_id = @DimensionId AND status = 'ACTIVE';
                """, new { DimensionId = dimensionId }));
        }

        Assert.Null(await _dimensionVersionRepository.GetActiveScaleAsync(dimensionId));
        Assert.True(await _dimensionVersionRepository.DimensionExistsAsync(dimensionId));
        Assert.False(await _dimensionVersionRepository.DimensionExistsAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_WithMeasurementReferencingRetiredVersion_ShouldPreserveHistoricalVersion()
    {
        // Semántica verificada de cambios concurrentes de DimensionVersion (sin inventar
        // garantías normativas): la FK solo exige existencia del par (dimension_id,
        // dimension_version_id), no que siga ACTIVE. Una medición resuelta sobre una versión
        // que se retira de forma concurrent conserva exactamente esa versión histórica
        // (coherente con RN-020/RN-031: la definición utilizada no cambia retroactivamente).
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();

        var scale = await _dimensionVersionRepository.GetActiveScaleAsync(dimensionId);
        Assert.Equal(versionId, scale!.DimensionVersionId);

        // La versión se retira entre la resolución y el INSERT (carrera concurrente simulada)
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE yusay.dimension_version SET status = 'RETIRED'
                WHERE dimension_version_id = @VersionId;
                """, new { VersionId = versionId }));
        }

        var checkIn = BuildCheckIn(userId,
            Measurement.Create(dimensionId, scale.DimensionVersionId, 3));
        await _repository.CreateAsync(checkIn);

        var retrieved = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        var measurement = Assert.Single(retrieved!.Measurements);
        Assert.Equal(versionId, measurement.DimensionVersionId); // procedencia histórica preservada
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenMeasurementInsertFails_ShouldRollbackCheckInCompletely()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var (foreignDimensionId, foreignVersionId) = await SeedDimensionAsync(0, 10, 1);
        var checkIn = BuildCheckIn(userId,
            Measurement.Create(dimensionId, versionId, 3),
            Measurement.Create(foreignDimensionId, foreignVersionId, 5));

        // Segunda medición inválida: su dimension_version_id no corresponde a su dimension_id
        // (la FK compuesta de la exige el par exacto), provocando fallo a mitad de transacción.
        var sabotaged = CheckIn.Rehydrate(
            checkIn.CheckInId, userId, checkIn.RecordedAt, checkIn.CreatedAt, null, 1, null,
            [
                Measurement.Create(dimensionId, versionId, 3),
                // Versión inexistente cuyo par (dimension_id, dimension_version_id) viola la FK compuesta
                Measurement.Create(foreignDimensionId, Guid.NewGuid(), 5)
            ]);

        await Assert.ThrowsAsync<PostgresException>(() =>
            _unitOfWork.ExecuteInTransactionAsync(tx => _repository.CreateAsync(sabotaged, tx)));

        // Rollback completo: ni el CheckIn ni la primera medición sobreviven
        var retrieved = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        Assert.Null(retrieved);

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var count = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.check_in WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
        Assert.Equal(0, count);
    }
}
