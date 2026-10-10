using Dapper;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.Tracking.Entities;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

/// <summary>
/// Edición física de CheckIn (F2b-1): sentencia condicional atómica de MP-PHYS-007
/// (revisión esperada + ventana absoluta de 168 horas + propiedad), actualización de
/// Measurements preservando sus DimensionVersion, rollback completo de la transacción y
/// resolución de escala por versión almacenada aunque esté RETIRED.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class CheckInUpdateRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly CheckInRepository _repository;
    private readonly DimensionVersionRepository _dimensionVersionRepository;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepository;

    public CheckInUpdateRepositoryTests(PostgreSqlFixture fixture)
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
            Email.Create($"checkin_update_repo_{Guid.NewGuid():N}@yusay.local"),
            DateTimeOffset.UtcNow.AddYears(-25));
        await _userAccountRepository.AddAsync(user);
        return user.Id;
    }

    private async Task<(Guid DimensionId, Guid VersionId)> SeedDimensionAsync(
        int minValue = 1, int maxValue = 5, int step = 1)
    {
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, 'dim_' || replace(@DimensionId::text, '-', ''), 'Dimensión de prueba', 'Escala de prueba.');
            """, new { DimensionId = dimensionId }));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, 1, 'Versión de prueba.', @MinValue, @MaxValue, @Step, 'ACTIVE');
            """, new
            {
                VersionId = versionId,
                DimensionId = dimensionId,
                MinValue = minValue,
                MaxValue = maxValue,
                Step = step
            }));

        return (dimensionId, versionId);
    }

    private static CheckIn BuildCheckIn(Guid userId, string? note, params Measurement[] measurements) =>
        CheckIn.Create(userId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, note, measurements);

    /// <summary>Desplaza created_at y recorded_at hacia el pasado conservando la ventana física.</summary>
    private async Task ShiftEditWindowAsync(Guid checkInId, int hours)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE yusay.check_in
            SET created_at = created_at - (@Hours::double precision * interval '1 hour'),
                recorded_at = recorded_at - (@Hours::double precision * interval '1 hour')
            WHERE check_in_id = @CheckInId;
            """, new { CheckInId = checkInId, Hours = hours }));
    }

    [Fact]
    public async Task TryUpdateOwnedAsync_WithMatchingRevisionInsideWindow_ShouldPersistIncrementAndTimestamp()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, "Original", Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var before = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        Assert.NotNull(before);
        var newRecordedAt = before!.RecordedAt.AddHours(-1);

        var applied = await _repository.TryUpdateOwnedAsync(
            checkIn.CheckInId, userId, expectedRevision: 1, newRecordedAt, "Editada");

        Assert.True(applied);
        var after = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        Assert.NotNull(after);
        Assert.Equal(2, after!.Revision);                 // incremento exacto de uno
        Assert.NotNull(after.UpdatedAt);                  // updated_at en la misma transacción
        Assert.Equal("Editada", after.Note);
        Assert.Equal(newRecordedAt, after.RecordedAt);    // ya en precisión de microsegundos: igualdad exacta
        Assert.Equal(before.CreatedAt, after.CreatedAt);  // created_at inmutable
        Assert.Equal(userId, after.UserId);               // user_id inmutable
    }

    [Fact]
    public async Task TryUpdateOwnedAsync_WithStaleRevision_ShouldReturnFalseAndPreserveState()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, "Original", Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var applied = await _repository.TryUpdateOwnedAsync(
            checkIn.CheckInId, userId, expectedRevision: 99,
            DateTimeOffset.UtcNow, "No debe persistirse");

        Assert.False(applied);
        var after = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        Assert.Equal(1, after!.Revision);
        Assert.Null(after.UpdatedAt);
        Assert.Equal("Original", after.Note);
    }

    [Fact]
    public async Task TryUpdateOwnedAsync_WhenEditWindowExpired_ShouldReturnFalseAndPreserveState()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, "Original", Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        // created_at queda 169 horas en el pasado: la ventana estricta de 168 h ya expiró
        await ShiftEditWindowAsync(checkIn.CheckInId, hours: 169);

        var before = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        var applied = await _repository.TryUpdateOwnedAsync(
            checkIn.CheckInId, userId, expectedRevision: 1, before!.RecordedAt, "No debe persistirse");

        Assert.False(applied);
        var after = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        Assert.Equal(1, after!.Revision);
        Assert.Null(after.UpdatedAt);
        Assert.Equal("Original", after.Note);
    }

    [Fact]
    public async Task TryUpdateOwnedAsync_ForForeignOwner_ShouldReturnFalseAndPreserveState()
    {
        var ownerId = await CreateTestUserAsync();
        var foreignUserId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(ownerId, "Original", Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var applied = await _repository.TryUpdateOwnedAsync(
            checkIn.CheckInId, foreignUserId, expectedRevision: 1,
            DateTimeOffset.UtcNow, "Intento ajeno");

        Assert.False(applied);
        var after = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, ownerId);
        Assert.Equal(1, after!.Revision);
        Assert.Null(after.UpdatedAt);
        Assert.Equal("Original", after.Note);
    }

    [Fact]
    public async Task TryUpdateOwnedMeasurementsAsync_ShouldUpdateValuesPreservingStoredVersions()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync(1, 9, 2);
        var checkIn = BuildCheckIn(userId, null, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var affected = await _repository.TryUpdateOwnedMeasurementsAsync(
            checkIn.CheckInId, [Measurement.Create(dimensionId, versionId, 7)]);

        Assert.Equal(1, affected);
        var after = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        var measurement = Assert.Single(after!.Measurements);
        Assert.Equal(7, measurement.Value);
        Assert.Equal(versionId, measurement.DimensionVersionId); // versión histórica intacta
        Assert.Equal(1, after.Revision);                         // sin incremento: operación separada
        Assert.Null(after.UpdatedAt);
    }

    [Fact]
    public async Task TryUpdateOwnedMeasurementsAsync_WithUnknownDimension_ShouldReportFewerAffectedRows()
    {
        // Contrato de conteo de MP-PHYS-007: el llamador detecta desajustes comparando
        // las filas afectadas con el total esperado antes de confirmar la transacción.
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, null, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var affected = await _repository.TryUpdateOwnedMeasurementsAsync(
            checkIn.CheckInId, [Measurement.Create(Guid.NewGuid(), versionId, 5)]);

        Assert.Equal(0, affected);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenFailureOccursAfterUpdate_ShouldRollbackEntirely()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, null, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var before = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        Assert.NotNull(before);

        // La actualización completa del CheckIn y sus Measurements ocurre y después falla
        // un paso posterior: la transacción debe deshacerlo todo.
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _unitOfWork.ExecuteInTransactionAsync(async transaction =>
            {
                var applied = await _repository.TryUpdateOwnedAsync(
                    checkIn.CheckInId, userId, expectedRevision: 1,
                    before!.RecordedAt.AddHours(-1), "Transaccional", transaction);
                Assert.True(applied);

                var updated = await _repository.TryUpdateOwnedMeasurementsAsync(
                    checkIn.CheckInId, [Measurement.Create(dimensionId, versionId, 5)], transaction);
                Assert.Equal(1, updated);

                throw new InvalidOperationException("fallo simulado tras la actualización");
            }));

        // Rollback completo: revisión, instante, nota y valores originales intactos
        var after = await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId);
        Assert.NotNull(after);
        Assert.Equal(before!.Revision, after!.Revision);
        Assert.Equal(1, after.Revision);
        Assert.Null(after.UpdatedAt);
        Assert.Equal(before.RecordedAt, after.RecordedAt);
        Assert.Equal(before.CreatedAt, after.CreatedAt);
        Assert.Equal(3, Assert.Single(after.Measurements).Value);
    }

    [Fact]
    public async Task GetScaleByVersionIdAsync_WithRetiredVersion_ShouldReturnFrozenScale()
    {
        var (dimensionId, versionId) = await SeedDimensionAsync(1, 9, 2);

        // Retirar la versión: V015 congela min/max/step y permite ACTIVE → RETIRED
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = @VersionId;
                """, new { VersionId = versionId }));
        }

        var scale = await _dimensionVersionRepository.GetScaleByVersionIdAsync(versionId);

        Assert.NotNull(scale);
        Assert.Equal(versionId, scale!.DimensionVersionId);
        Assert.Equal(1, scale.MinValue);
        Assert.Equal(9, scale.MaxValue);
        Assert.Equal(2, scale.Step);

        // La resolución por estado ACTIVE ya no encuentra nada: la edición usa la versión almacenada
        Assert.Null(await _dimensionVersionRepository.GetActiveScaleAsync(dimensionId));
    }

    [Fact]
    public async Task GetScaleByVersionIdAsync_WithUnknownVersion_ShouldReturnNull()
    {
        Assert.Null(await _dimensionVersionRepository.GetScaleByVersionIdAsync(Guid.NewGuid()));
    }
}
