using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Queries.ListCheckIns;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

public class ListCheckInsUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeCheckInRepository _checkInRepo = new() { DatabaseTimestamp = Now };
    private readonly Guid _dimensionId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    public ListCheckInsUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_ownerId);
    }

    private ListCheckInsUseCase CreateUseCase() => new(_currentUser, _checkInRepo);

    private Guid ArrangeCheckIn(DateTimeOffset recordedAt, string? note = "Original", int revision = 1)
    {
        var checkIn = CheckIn.Rehydrate(
            Guid.NewGuid(),
            _ownerId,
            recordedAt,
            recordedAt,
            updatedAt: null,
            revision,
            note,
            measurements: [Measurement.Create(_dimensionId, _versionId, 3)]);
        _checkInRepo.CheckIns.Add(checkIn);
        return checkIn.CheckInId;
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorizedWithoutQuerying()
    {
        ArrangeCheckIn(Now);
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new ListCheckInsQuery(null, null)));

        Assert.Equal(0, _checkInRepo.ListOwnedCalls);
        Assert.Single(_checkInRepo.CheckIns);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task ExecuteAsync_WithValidLimit_ShouldRequestLimitPlusOneRows(int limit)
    {
        ArrangeCheckIn(Now);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListCheckInsQuery(limit, null));

        Assert.Equal(limit + 1, _checkInRepo.LastFetchLimit);
        Assert.Single(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ExecuteAsync_WithOutOfRangeLimit_ShouldThrowValidationWithoutQuerying(int limit)
    {
        ArrangeCheckIn(Now);
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new ListCheckInsQuery(limit, null)));

        Assert.Contains("limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, _checkInRepo.ListOwnedCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutLimit_ShouldApplyDefaultLimit()
    {
        ArrangeCheckIn(Now);
        var useCase = CreateUseCase();

        await useCase.ExecuteAsync(new ListCheckInsQuery(null, null));

        Assert.Equal(ListCheckInsUseCase.DefaultLimit + 1, _checkInRepo.LastFetchLimit);
    }

    [Fact]
    public async Task ExecuteAsync_WithMalformedCursor_ShouldThrowUniformValidationWithoutQuerying()
    {
        ArrangeCheckIn(Now);
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new ListCheckInsQuery(null, "no-es-base64!!")));

        Assert.Equal("El cursor proporcionado no es válido.", exception.Message);
        Assert.Equal(0, _checkInRepo.ListOwnedCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidCursor_ShouldForwardDecodedKey()
    {
        ArrangeCheckIn(Now);
        var expectedRecordedAt = Now.AddHours(-2);
        var expectedCheckInId = Guid.NewGuid();
        var cursor = CheckInListCursor.Encode(expectedRecordedAt, expectedCheckInId);
        var useCase = CreateUseCase();

        await useCase.ExecuteAsync(new ListCheckInsQuery(null, cursor));

        Assert.Equal(expectedRecordedAt, _checkInRepo.LastCursorRecordedAt);
        Assert.Equal(expectedCheckInId, _checkInRepo.LastCursorCheckInId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMoreRowsThanLimit_ShouldTrimAndAnchorCursorOnLastKept()
    {
        var oldest = ArrangeCheckIn(Now.AddHours(-2));
        var lastKept = ArrangeCheckIn(Now.AddHours(-1));
        var newest = ArrangeCheckIn(Now);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListCheckInsQuery(2, null));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(newest, result.Items[0].CheckInId);
        Assert.Equal(lastKept, result.Items[1].CheckInId);
        Assert.NotNull(result.NextCursor);

        var key = CheckInListCursor.Decode(result.NextCursor);
        Assert.NotNull(key);
        Assert.Equal(lastKept, key.CheckInId);
        Assert.Equal(Now.AddHours(-1), key.RecordedAt);
        Assert.NotEqual(oldest, key.CheckInId);
    }

    [Fact]
    public async Task ExecuteAsync_WithTiedRecordedAt_ShouldOrderByIdDescendingAndAnchorCursorOnLastKept()
    {
        var tieTime = Now.AddMinutes(-30);
        var firstId = ArrangeCheckIn(tieTime);
        var secondId = ArrangeCheckIn(tieTime);
        var expectedFirst = new[] { firstId, secondId }.OrderByDescending(id => id).First();
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListCheckInsQuery(1, null));

        Assert.Single(result.Items);
        Assert.Equal(expectedFirst, result.Items[0].CheckInId);

        var key = CheckInListCursor.Decode(result.NextCursor);
        Assert.NotNull(key);
        Assert.Equal(expectedFirst, key.CheckInId);
        Assert.Equal(tieTime, key.RecordedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRowsFitLimit_ShouldReturnNullCursor()
    {
        ArrangeCheckIn(Now.AddHours(-1));
        ArrangeCheckIn(Now);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListCheckInsQuery(20, null));

        Assert.Equal(2, result.Items.Count);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCollectionIsEmpty_ShouldReturnEmptyItemsAndNullCursor()
    {
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListCheckInsQuery(null, null));

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProjectEveryItemField()
    {
        var recordedAt = Now.AddHours(-3);
        var checkInId = ArrangeCheckIn(recordedAt, note: "Con nota", revision: 1);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListCheckInsQuery(null, null));

        var item = Assert.Single(result.Items);
        Assert.Equal(checkInId, item.CheckInId);
        Assert.Equal(recordedAt, item.RecordedAt);
        Assert.Equal(recordedAt, item.CreatedAt);
        Assert.Null(item.UpdatedAt);
        Assert.Equal(1, item.Revision);
        Assert.Equal("Con nota", item.Note);
        var measurement = Assert.Single(item.Measurements);
        Assert.Equal(_dimensionId, measurement.DimensionId);
        Assert.Equal(_versionId, measurement.DimensionVersionId);
        Assert.Equal(3, measurement.Value);
    }
}
