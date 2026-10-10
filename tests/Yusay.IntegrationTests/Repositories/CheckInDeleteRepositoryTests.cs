using Dapper;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.Tracking.Entities;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

/// <summary>
/// Eliminación física de CheckIn (F2b-2 / OQ-DOM-009): sentencia condicional única con
/// check_in_id + user_id + revision sin ventana temporal, cascada FK sobre Measurements
/// y check_in_context_tag, preservación ante revisión vieja o propietario ajeno y
/// rollback completo de la supresión con sus dependientes.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class CheckInDeleteRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly CheckInRepository _repository;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepository;

    public CheckInDeleteRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _repository = new CheckInRepository(fixture.ConnectionFactory);
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepository = new UserAccountRepository(fixture.ConnectionFactory);
    }

    private async Task<Guid> CreateTestUserAsync()
    {
        var user = UserAccount.Create(
            Email.Create($"checkin_delete_repo_{Guid.NewGuid():N}@yusay.local"),
            DateTimeOffset.UtcNow.AddYears(-25));
        await _userAccountRepository.AddAsync(user);
        return user.Id;
    }

    private async Task<(Guid DimensionId, Guid VersionId)> SeedDimensionAsync()
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
            VALUES (@VersionId, @DimensionId, 1, 'Versión de prueba.', 1, 5, 1, 'ACTIVE');
            """, new { VersionId = versionId, DimensionId = dimensionId }));

        return (dimensionId, versionId);
    }

    private async Task<Guid> SeedContextTagAsync()
    {
        var contextTagId = Guid.NewGuid();
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.context_tag (context_tag_id, code, name)
            VALUES (@ContextTagId, 'tag_' || replace(@ContextTagId::text, '-', ''), 'Etiqueta de prueba.');
            """, new { ContextTagId = contextTagId }));
        return contextTagId;
    }

    private static CheckIn BuildCheckIn(Guid userId, params Measurement[] measurements) =>
        CheckIn.Create(userId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Original", measurements);

    private async Task<int> CountAsync(string sql, object? parameters = null)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters));
    }

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
    public async Task TryDeleteOwnedAsync_WithMatchingRevision_ShouldDeleteRowAndCascadeDependents()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var contextTagId = await SeedContextTagAsync();
        var checkIn = BuildCheckIn(userId, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.check_in_context_tag (check_in_id, context_tag_id)
                VALUES (@CheckInId, @ContextTagId);
                """, new { CheckInId = checkIn.CheckInId, ContextTagId = contextTagId }));
        }

        var deleted = await _repository.TryDeleteOwnedAsync(checkIn.CheckInId, userId, expectedRevision: 1);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId));

        // Cascada FK existente: measurements y check_in_context_tag suprimidos en la misma
        // sentencia; la etiqueta de contexto compartible permanece intacta (FK RESTRICT).
        Assert.Equal(0, await CountAsync(
            "SELECT count(*) FROM yusay.measurement WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
        Assert.Equal(0, await CountAsync(
            "SELECT count(*) FROM yusay.check_in_context_tag WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.context_tag WHERE context_tag_id = @Id;",
            new { Id = contextTagId }));
    }

    [Fact]
    public async Task TryDeleteOwnedAsync_WithStaleRevision_ShouldReturnFalseAndPreserveEverything()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var contextTagId = await SeedContextTagAsync();
        var checkIn = BuildCheckIn(userId, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.check_in_context_tag (check_in_id, context_tag_id)
                VALUES (@CheckInId, @ContextTagId);
                """, new { CheckInId = checkIn.CheckInId, ContextTagId = contextTagId }));
        }

        var deleted = await _repository.TryDeleteOwnedAsync(checkIn.CheckInId, userId, expectedRevision: 99);

        Assert.False(deleted);
        Assert.NotNull(await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.measurement WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.check_in_context_tag WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
    }

    [Fact]
    public async Task TryDeleteOwnedAsync_ForForeignOwner_ShouldReturnFalseAndPreserveRow()
    {
        var ownerId = await CreateTestUserAsync();
        var foreignUserId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(ownerId, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        var deleted = await _repository.TryDeleteOwnedAsync(checkIn.CheckInId, foreignUserId, expectedRevision: 1);

        Assert.False(deleted);
        Assert.NotNull(await _repository.GetByIdOwnedAsync(checkIn.CheckInId, ownerId));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.measurement WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
    }

    [Fact]
    public async Task TryDeleteOwnedAsync_WhenCheckInIsOlderThan168Hours_ShouldDelete()
    {
        // Sin predicado temporal (OQ-DOM-009): un registro de 200 horas se elimina igual.
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = BuildCheckIn(userId, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);
        await ShiftEditWindowAsync(checkIn.CheckInId, hours: 200);

        var deleted = await _repository.TryDeleteOwnedAsync(checkIn.CheckInId, userId, expectedRevision: 1);

        Assert.True(deleted);
        Assert.Null(await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId));
        Assert.Equal(0, await CountAsync(
            "SELECT count(*) FROM yusay.measurement WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenFailureOccursAfterDelete_ShouldRollbackCascade()
    {
        // Rollback completo: la supresión del CheckIn y la de sus dependientes por cascada
        // se deshacen íntegramente cuando la transacción aborta.
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var contextTagId = await SeedContextTagAsync();
        var checkIn = BuildCheckIn(userId, Measurement.Create(dimensionId, versionId, 3));
        await _repository.CreateAsync(checkIn);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.check_in_context_tag (check_in_id, context_tag_id)
                VALUES (@CheckInId, @ContextTagId);
                """, new { CheckInId = checkIn.CheckInId, ContextTagId = contextTagId }));
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _unitOfWork.ExecuteInTransactionAsync(async transaction =>
            {
                var deleted = await _repository.TryDeleteOwnedAsync(
                    checkIn.CheckInId, userId, expectedRevision: 1, transaction);
                Assert.True(deleted);
                Assert.Null(await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId, transaction));

                throw new InvalidOperationException("fallo simulado tras la eliminación");
            }));

        Assert.NotNull(await _repository.GetByIdOwnedAsync(checkIn.CheckInId, userId));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.measurement WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.check_in_context_tag WHERE check_in_id = @Id;",
            new { Id = checkIn.CheckInId }));
    }
}
