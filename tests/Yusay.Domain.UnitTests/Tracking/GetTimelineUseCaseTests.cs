using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Queries.GetTimeline;
using Yusay.Domain.UnitTests.Identity.Fakes;
using Yusay.Domain.UnitTests.Tracking.Fakes;

namespace Yusay.Domain.UnitTests.Tracking;

public class GetTimelineUseCaseTests
{
    private static readonly DateTimeOffset BaseTime = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeTimelineRepository _timelineRepository = new();

    public GetTimelineUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_ownerId);
    }

    private GetTimelineUseCase CreateUseCase() => new(_currentUser, _timelineRepository);

    private TimelineItemDto AddAssessmentEvent(
        DateTimeOffset occurredAt,
        Guid? eventId = null,
        Guid? userId = null,
        int score = 10,
        string label = "Moderada")
    {
        var id = eventId ?? Guid.NewGuid();
        var targetUser = userId ?? _ownerId;
        var dto = new TimelineItemDto(
            id,
            "assessment",
            occurredAt,
            new TimelineAssessmentDto(
                id,
                Guid.NewGuid(),
                "PHQ-9",
                "Patient Health Questionnaire",
                Guid.NewGuid(),
                1,
                score,
                new TimelineInterpretationDto(Guid.NewGuid(), label)),
            null);

        _timelineRepository.Items.Add((targetUser, dto));
        return dto;
    }

    private TimelineItemDto AddCheckInEvent(
        DateTimeOffset occurredAt,
        Guid? eventId = null,
        Guid? userId = null,
        string? note = null,
        int moodValue = 3)
    {
        var id = eventId ?? Guid.NewGuid();
        var targetUser = userId ?? _ownerId;
        var dto = new TimelineItemDto(
            id,
            "check_in",
            occurredAt,
            null,
            new TimelineCheckInDto(
                id,
                note,
                new List<TimelineMeasurementDto>
                {
                    new(Guid.NewGuid(), "mood", "Mood", Guid.NewGuid(), moodValue)
                }));

        _timelineRepository.Items.Add((targetUser, dto));
        return dto;
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new GetTimelineQuery(null, null, null, null, null)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task ExecuteAsync_WithValidLimit_ShouldReturnItemsAndNoNextCursorWhenUnderLimit(int limit)
    {
        AddAssessmentEvent(BaseTime);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new GetTimelineQuery(limit, null, null, null, null));

        Assert.Single(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ExecuteAsync_WithOutOfRangeLimit_ShouldThrowValidation(int limit)
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new GetTimelineQuery(limit, null, null, null, null)));

        Assert.Contains("limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutLimit_ShouldApplyDefaultLimitTwenty()
    {
        for (var i = 0; i < 25; i++)
        {
            AddAssessmentEvent(BaseTime.AddMinutes(-i));
        }

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetTimelineQuery(null, null, null, null, null));

        Assert.Equal(GetTimelineUseCase.DefaultLimit, result.Items.Count);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task ExecuteAsync_WithFromLaterThanTo_ShouldThrowValidation()
    {
        var useCase = CreateUseCase();
        var from = BaseTime;
        var to = BaseTime.AddHours(-1);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new GetTimelineQuery(null, null, from, to, null)));

        Assert.Equal("El parámetro 'from' no puede ser posterior al parámetro 'to'.", exception.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("assessment_attempt")]
    [InlineData("checkin")]
    public async Task ExecuteAsync_WithInvalidType_ShouldThrowValidation(string invalidType)
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new GetTimelineQuery(null, null, null, null, invalidType)));

        Assert.Equal("El parámetro 'type' debe ser 'assessment' o 'check_in'.", exception.Message);
    }

    [Theory]
    [InlineData("assessment")]
    [InlineData("check_in")]
    [InlineData("ASSESSMENT")]
    [InlineData("CHECK_IN")]
    public async Task ExecuteAsync_WithValidType_ShouldFilterByType(string typeFilter)
    {
        AddAssessmentEvent(BaseTime.AddMinutes(-10));
        AddCheckInEvent(BaseTime.AddMinutes(-5));

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetTimelineQuery(null, null, null, null, typeFilter));

        var expectedType = typeFilter.ToLowerInvariant();
        Assert.Single(result.Items);
        Assert.Equal(expectedType, result.Items[0].Type);
    }

    [Fact]
    public async Task ExecuteAsync_WithFromAndTo_ShouldFilterByTimeRange()
    {
        AddAssessmentEvent(BaseTime.AddHours(-5)); // fuera
        var inRange1 = AddCheckInEvent(BaseTime.AddHours(-3)); // dentro
        var inRange2 = AddAssessmentEvent(BaseTime.AddHours(-2)); // dentro
        AddCheckInEvent(BaseTime.AddHours(1)); // fuera

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetTimelineQuery(
            null,
            null,
            BaseTime.AddHours(-4),
            BaseTime.AddHours(-1),
            null));

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(inRange2.EventId, result.Items[0].EventId);
        Assert.Equal(inRange1.EventId, result.Items[1].EventId);
    }

    [Fact]
    public async Task ExecuteAsync_WithMalformedCursor_ShouldThrowValidation()
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new GetTimelineQuery(null, "invalid-cursor-garbage", null, null, null)));

        Assert.Equal("El cursor proporcionado no es válido.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmpty_ShouldReturnEmptyList()
    {
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new GetTimelineQuery(null, null, null, null, null));

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIsolateItemsByOwner()
    {
        var otherUser = Guid.NewGuid();
        AddAssessmentEvent(BaseTime, userId: otherUser);
        AddCheckInEvent(BaseTime.AddMinutes(-1), userId: otherUser);
        var myEvent = AddAssessmentEvent(BaseTime.AddMinutes(-5), userId: _ownerId);

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetTimelineQuery(null, null, null, null, null));

        Assert.Single(result.Items);
        Assert.Equal(myEvent.EventId, result.Items[0].EventId);
    }

    [Fact]
    public async Task ExecuteAsync_WithKeysetPagination_ShouldNavigateDeterministically()
    {
        var events = new List<TimelineItemDto>();
        for (var i = 0; i < 5; i++)
        {
            if (i % 2 == 0)
            {
                events.Add(AddAssessmentEvent(BaseTime.AddMinutes(-i * 10)));
            }
            else
            {
                events.Add(AddCheckInEvent(BaseTime.AddMinutes(-i * 10)));
            }
        }

        var useCase = CreateUseCase();

        // Página 1 (limit 2)
        var page1 = await useCase.ExecuteAsync(new GetTimelineQuery(2, null, null, null, null));
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.Equal(events[0].EventId, page1.Items[0].EventId);
        Assert.Equal(events[1].EventId, page1.Items[1].EventId);

        // Página 2 (limit 2 con cursor)
        var page2 = await useCase.ExecuteAsync(new GetTimelineQuery(2, page1.NextCursor, null, null, null));
        Assert.Equal(2, page2.Items.Count);
        Assert.NotNull(page2.NextCursor);
        Assert.Equal(events[2].EventId, page2.Items[0].EventId);
        Assert.Equal(events[3].EventId, page2.Items[1].EventId);

        // Página 3 (limit 2 con cursor)
        var page3 = await useCase.ExecuteAsync(new GetTimelineQuery(2, page2.NextCursor, null, null, null));
        Assert.Single(page3.Items);
        Assert.Null(page3.NextCursor);
        Assert.Equal(events[4].EventId, page3.Items[0].EventId);
    }

    [Fact]
    public async Task ExecuteAsync_WithExactTimestampTie_ShouldBreakTieByEventTypeAndEventId()
    {
        var sameTime = BaseTime;
        var checkInId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var assessmentId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        // "check_in" > "assessment" en orden DESC ordinal
        var checkIn = AddCheckInEvent(sameTime, eventId: checkInId);
        var assessment = AddAssessmentEvent(sameTime, eventId: assessmentId);

        var useCase = CreateUseCase();

        // Página 1 de tamaño 1: debe devolver check_in primero por type DESC
        var page1 = await useCase.ExecuteAsync(new GetTimelineQuery(1, null, null, null, null));
        Assert.Single(page1.Items);
        Assert.Equal(checkIn.EventId, page1.Items[0].EventId);
        Assert.Equal("check_in", page1.Items[0].Type);
        Assert.NotNull(page1.NextCursor);

        // Página 2 con cursor: debe continuar con assessment
        var page2 = await useCase.ExecuteAsync(new GetTimelineQuery(1, page1.NextCursor, null, null, null));
        Assert.Single(page2.Items);
        Assert.Equal(assessment.EventId, page2.Items[0].EventId);
        Assert.Equal("assessment", page2.Items[0].Type);
        Assert.Null(page2.NextCursor);
    }
}
