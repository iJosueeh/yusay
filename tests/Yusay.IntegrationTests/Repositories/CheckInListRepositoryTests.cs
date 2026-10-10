using Dapper;
using Yusay.Domain.Identity.Entities;
using Yusay.Domain.Identity.ValueObjects;
using Yusay.Domain.Tracking.Entities;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

[Collection("DatabaseCollection")]
public sealed class CheckInListRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly CheckInRepository _repository;
    private readonly UserAccountRepository _userAccountRepository;

    public CheckInListRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _repository = new CheckInRepository(fixture.ConnectionFactory);
        _userAccountRepository = new UserAccountRepository(fixture.ConnectionFactory);
    }

    private async Task<Guid> CreateTestUserAsync()
    {
        var user = UserAccount.Create(
            Email.Create($"checkin_list_repo_{Guid.NewGuid():N}@yusay.local"),
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

    private async Task<CheckIn> CreateCheckInAsync(
        Guid userId, DateTimeOffset recordedAt, params Measurement[] measurements)
    {
        var checkIn = CheckIn.Create(userId, recordedAt, DateTimeOffset.UtcNow, "Original", measurements);
        await _repository.CreateAsync(checkIn);
        return checkIn;
    }

    private async Task<int> CountAsync(string sql, object? parameters = null)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, parameters));
    }

    [Fact]
    public async Task ListOwnedPageAsync_WithTiedRecordedAt_ShouldOrderDeterministicallyAndStayStableAcrossCalls()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-4);
        var oldest = await CreateCheckInAsync(userId, baseTime, Measurement.Create(dimensionId, versionId, 1));
        var tieA = await CreateCheckInAsync(
            userId, baseTime.AddMinutes(10), Measurement.Create(dimensionId, versionId, 2));
        var tieB = await CreateCheckInAsync(
            userId, baseTime.AddMinutes(10), Measurement.Create(dimensionId, versionId, 3));
        var newest = await CreateCheckInAsync(
            userId, baseTime.AddMinutes(20), Measurement.Create(dimensionId, versionId, 4));

        var page = await _repository.ListOwnedPageAsync(userId, fetchLimit: 10, null, null);
        var secondPass = await _repository.ListOwnedPageAsync(userId, fetchLimit: 10, null, null);

        Assert.Equal(4, page.Count);
        Assert.Equal(
            page.Select(c => c.CheckInId).ToArray(),
            secondPass.Select(c => c.CheckInId).ToArray());
        Assert.Equal(newest.CheckInId, page[0].CheckInId);
        Assert.Equal(oldest.CheckInId, page[3].CheckInId);
        Assert.Equal(page[1].RecordedAt, page[2].RecordedAt);
        Assert.True(
            new HashSet<Guid> { tieA.CheckInId, tieB.CheckInId }
                .SetEquals(new[] { page[1].CheckInId, page[2].CheckInId }));
        for (var index = 1; index < page.Count; index++)
        {
            Assert.True(page[index - 1].RecordedAt >= page[index].RecordedAt);
        }
    }

    [Fact]
    public async Task ListOwnedPageAsync_KeysetWalk_ShouldVisitEveryRowExactlyOnceEvenWithTies()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-5);
        var expected = new HashSet<Guid>();
        for (var index = 0; index < 5; index++)
        {
            var recordedAt = index == 2 ? baseTime.AddMinutes(10) : baseTime.AddMinutes(index * 10);
            var checkIn = await CreateCheckInAsync(
                userId, recordedAt, Measurement.Create(dimensionId, versionId, 3));
            expected.Add(checkIn.CheckInId);
        }

        var visited = new List<CheckIn>();
        DateTimeOffset? cursorRecordedAt = null;
        Guid? cursorCheckInId = null;
        while (true)
        {
            var page = await _repository.ListOwnedPageAsync(
                userId, fetchLimit: 2, cursorRecordedAt, cursorCheckInId);
            if (page.Count == 0)
            {
                break;
            }

            visited.AddRange(page);
            cursorRecordedAt = page[^1].RecordedAt;
            cursorCheckInId = page[^1].CheckInId;
        }

        Assert.Equal(5, visited.Count);
        Assert.True(expected.SetEquals(visited.Select(c => c.CheckInId)));
        for (var index = 1; index < visited.Count; index++)
        {
            Assert.True(visited[index - 1].RecordedAt >= visited[index].RecordedAt);
        }
    }

    [Fact]
    public async Task ListOwnedPageAsync_WithAnchorCursor_ShouldExcludeAnchorAndEverythingNewer()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-4);
        var rows = new List<CheckIn>();
        for (var index = 0; index < 4; index++)
        {
            rows.Add(await CreateCheckInAsync(
                userId, baseTime.AddMinutes(index * 10), Measurement.Create(dimensionId, versionId, 3)));
        }

        var ordered = rows.OrderByDescending(c => c.RecordedAt).ToArray();
        var anchor = ordered[2];

        var page = await _repository.ListOwnedPageAsync(
            userId, fetchLimit: 10, anchor.RecordedAt, anchor.CheckInId);

        var expected = ordered.Skip(3).Select(c => c.CheckInId).ToArray();
        Assert.Equal(expected, page.Select(c => c.CheckInId).ToArray());
    }

    [Fact]
    public async Task ListOwnedPageAsync_ShouldExcludeForeignOwnersAndDeletedRows()
    {
        var ownerId = await CreateTestUserAsync();
        var foreignUserId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-3);
        var ownKept = await CreateCheckInAsync(
            ownerId, baseTime.AddMinutes(10), Measurement.Create(dimensionId, versionId, 3));
        var ownDeleted = await CreateCheckInAsync(
            ownerId, baseTime, Measurement.Create(dimensionId, versionId, 3));
        var foreign = await CreateCheckInAsync(
            foreignUserId, baseTime.AddMinutes(20), Measurement.Create(dimensionId, versionId, 3));

        var deleted = await _repository.TryDeleteOwnedAsync(
            ownDeleted.CheckInId, ownerId, expectedRevision: 1);
        Assert.True(deleted);

        var ownerPage = await _repository.ListOwnedPageAsync(ownerId, fetchLimit: 10, null, null);
        var foreignPage = await _repository.ListOwnedPageAsync(foreignUserId, fetchLimit: 10, null, null);

        Assert.Equal(new[] { ownKept.CheckInId }, ownerPage.Select(c => c.CheckInId).ToArray());
        Assert.Equal(new[] { foreign.CheckInId }, foreignPage.Select(c => c.CheckInId).ToArray());
    }

    [Fact]
    public async Task ListOwnedPageAsync_ShouldAggregateMeasurementsOrderedByDimensionId()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionA, versionA) = await SeedDimensionAsync();
        var (dimensionB, versionB) = await SeedDimensionAsync();
        var recordedAt = new DateTimeOffset(
            (DateTimeOffset.UtcNow.AddHours(-2).Ticks / 10) * 10, TimeSpan.Zero);
        var checkIn = await CreateCheckInAsync(
            userId,
            recordedAt,
            Measurement.Create(dimensionB, versionB, 5),
            Measurement.Create(dimensionA, versionA, 2));

        var page = await _repository.ListOwnedPageAsync(userId, fetchLimit: 10, null, null);

        var item = Assert.Single(page);
        Assert.Equal(checkIn.CheckInId, item.CheckInId);
        Assert.Equal(userId, item.UserId);
        Assert.Equal(recordedAt, item.RecordedAt);
        Assert.Equal(2, item.Measurements.Count);

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var expectedOrder = (await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT dimension_id FROM yusay.measurement WHERE check_in_id = @Id ORDER BY dimension_id;",
            new { Id = checkIn.CheckInId }))).ToArray();
        Assert.Equal(expectedOrder, item.Measurements.Select(m => m.DimensionId).ToArray());

        var valueByDimension = item.Measurements.ToDictionary(m => m.DimensionId, m => m.Value);
        Assert.Equal(2, valueByDimension[dimensionA]);
        Assert.Equal(5, valueByDimension[dimensionB]);
        Assert.All(item.Measurements, measurement => Assert.True(measurement.DimensionVersionId != Guid.Empty));
    }

    [Fact]
    public async Task ListOwnedPageAsync_WithFetchLimit_ShouldReturnNewestRowsUpToLimit()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-3);
        var rows = new List<CheckIn>();
        for (var index = 0; index < 3; index++)
        {
            rows.Add(await CreateCheckInAsync(
                userId, baseTime.AddMinutes(index * 10), Measurement.Create(dimensionId, versionId, 3)));
        }

        var limitTwo = await _repository.ListOwnedPageAsync(userId, fetchLimit: 2, null, null);
        var limitOne = await _repository.ListOwnedPageAsync(userId, fetchLimit: 1, null, null);

        Assert.Equal(2, limitTwo.Count);
        Assert.Equal(rows[2].CheckInId, limitTwo[0].CheckInId);
        Assert.Equal(rows[1].CheckInId, limitTwo[1].CheckInId);
        Assert.Single(limitOne);
        Assert.Equal(rows[2].CheckInId, limitOne[0].CheckInId);
    }

    [Fact]
    public async Task ListOwnedPageAsync_WhenStoredRowHasNoMeasurements_ShouldThrowRn030IntegrityViolation()
    {
        var userId = await CreateTestUserAsync();
        var checkInId = Guid.NewGuid();
        var recordedAt = DateTimeOffset.UtcNow.AddHours(-1);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at, revision, note)
                VALUES (@CheckInId, @UserId, @RecordedAt, @CreatedAt, 1, NULL);
                """, new
                {
                    CheckInId = checkInId,
                    UserId = userId,
                    RecordedAt = recordedAt,
                    CreatedAt = recordedAt
                }));
        }

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _repository.ListOwnedPageAsync(userId, fetchLimit: 10, null, null));

        Assert.Contains("RN-030", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListOwnedPageAsync_WhenNoRowsMatch_ShouldReturnEmpty()
    {
        var userId = await CreateTestUserAsync();
        var futureCursor = DateTimeOffset.UtcNow.AddDays(1);

        var empty = await _repository.ListOwnedPageAsync(userId, fetchLimit: 10, null, null);
        var beyondEnd = await _repository.ListOwnedPageAsync(
            userId, fetchLimit: 10, futureCursor, Guid.NewGuid());

        Assert.Empty(empty);
        Assert.Empty(beyondEnd);
    }

    [Fact]
    public async Task ListOwnedPageAsync_ExplainAnalyze_ShouldCompleteWithoutPlanRequirements()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        await CreateCheckInAsync(userId, DateTimeOffset.UtcNow.AddHours(-1),
            Measurement.Create(dimensionId, versionId, 3));

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var plan = await connection.QueryAsync<string>(new CommandDefinition("""
            EXPLAIN (ANALYZE, BUFFERS)
            SELECT check_in_id
            FROM yusay.check_in
            WHERE user_id = @OwnerId
            ORDER BY recorded_at DESC, check_in_id DESC
            LIMIT 21;
            """, new { OwnerId = userId }));

        Assert.NotEmpty(plan);
        Assert.Contains(plan, line => line.Contains("Execution Time", StringComparison.Ordinal));
        Assert.Contains(plan, line => line.Contains("actual time", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ListOwnedPageAsync_StatementAndMeasurements_ShouldLeaveDataUnchanged()
    {
        var userId = await CreateTestUserAsync();
        var (dimensionId, versionId) = await SeedDimensionAsync();
        var checkIn = await CreateCheckInAsync(
            userId, DateTimeOffset.UtcNow.AddHours(-1), Measurement.Create(dimensionId, versionId, 3));

        await _repository.ListOwnedPageAsync(userId, fetchLimit: 10, null, null);

        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.check_in WHERE check_in_id = @Id;", new { Id = checkIn.CheckInId }));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.measurement WHERE check_in_id = @Id;", new { Id = checkIn.CheckInId }));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.check_in WHERE check_in_id = @Id AND revision = 1;",
            new { Id = checkIn.CheckInId }));
    }
}
