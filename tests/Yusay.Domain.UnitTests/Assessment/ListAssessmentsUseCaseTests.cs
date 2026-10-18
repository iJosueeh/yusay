using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Queries.ListAssessments;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public class ListAssessmentsUseCaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeAssessmentAttemptRepository _attemptRepo = new();
    private readonly FixedTimeProvider _timeProvider = new(Now);
    private readonly Guid _instrumentId = Guid.NewGuid();
    private readonly Guid _versionId = Guid.NewGuid();

    public ListAssessmentsUseCaseTests()
    {
        _currentUser = new FakeCurrentUser(_ownerId);
        _attemptRepo.VersionNumbers[_versionId] = 1;
        _attemptRepo.VersionInstrumentCode[_versionId] = "PHQ-9";
        _attemptRepo.VersionInstrumentName[_versionId] = "Patient Health Questionnaire";
    }

    private ListAssessmentsUseCase CreateUseCase() =>
        new(_currentUser, _attemptRepo, _timeProvider);

    private AssessmentAttempt ArrangeAttempt(
        DateTimeOffset startedAt,
        AssessmentAttemptStatus status = AssessmentAttemptStatus.InProgress,
        Guid? userId = null,
        DateTimeOffset? expiresAt = null)
    {
        var targetUserId = userId ?? _ownerId;
        var attempt = AssessmentAttempt.Create(targetUserId, _instrumentId, _versionId, startedAt);

        if (expiresAt.HasValue)
        {
            // Set specific expiration if needed
        }

        if (status == AssessmentAttemptStatus.Submitted)
        {
            var interpId = Guid.NewGuid();
            _attemptRepo.VersionInterpretations[_versionId] = new List<InterpretationDataDto>
            {
                new(interpId, "Leve", "Depresión leve", null, 5, 9)
            };
            _attemptRepo.AttemptResults[attempt.AttemptId] = (7, interpId, startedAt.AddMinutes(15));
            attempt.Submit(startedAt.AddMinutes(15));
        }

        _attemptRepo.Attempts.Add(attempt);
        return attempt;
    }

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedIdentity_ShouldThrowUnauthorized()
    {
        ArrangeAttempt(Now);
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => useCase.ExecuteAsync(new ListAssessmentsQuery(null, null)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task ExecuteAsync_WithValidLimit_ShouldReturnItemsAndNoNextCursorWhenUnderLimit(int limit)
    {
        ArrangeAttempt(Now);
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListAssessmentsQuery(limit, null));

        Assert.Single(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ExecuteAsync_WithOutOfRangeLimit_ShouldThrowValidation(int limit)
    {
        ArrangeAttempt(Now);
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new ListAssessmentsQuery(limit, null)));

        Assert.Contains("limit", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_WithoutLimit_ShouldApplyDefaultLimitTwenty()
    {
        for (var i = 0; i < 25; i++)
        {
            ArrangeAttempt(Now.AddMinutes(-i));
        }

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new ListAssessmentsQuery(null, null));

        Assert.Equal(ListAssessmentsUseCase.DefaultLimit, result.Items.Count);
        Assert.NotNull(result.NextCursor);
    }

    [Fact]
    public async Task ExecuteAsync_WithMalformedCursor_ShouldThrowValidation()
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => useCase.ExecuteAsync(new ListAssessmentsQuery(20, "invalid-cursor-garbage")));

        Assert.Equal("El cursor proporcionado no es válido.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoAttempts_ShouldReturnEmptyList()
    {
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new ListAssessmentsQuery(null, null));

        Assert.Empty(result.Items);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptInProgressAndExpired_ShouldProjectAsExpiredWithoutModifyingRepository()
    {
        // Inició hace 800 horas (venció hace 80 horas)
        var startedAt = Now.AddHours(-800);
        var attempt = ArrangeAttempt(startedAt, AssessmentAttemptStatus.InProgress);

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new ListAssessmentsQuery(null, null));

        Assert.Single(result.Items);
        Assert.Equal("EXPIRED", result.Items[0].Status);
        Assert.Null(result.Items[0].Result);

        // La base de datos / repositorio no debe haber sido modificada en el listado
        Assert.Equal(AssessmentAttemptStatus.InProgress, attempt.Status);
        Assert.DoesNotContain(attempt.AttemptId, _attemptRepo.ExpiredAttempts);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptSubmitted_ShouldIncludeResultSummary()
    {
        var attempt = ArrangeAttempt(Now.AddHours(-1), AssessmentAttemptStatus.Submitted);

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new ListAssessmentsQuery(null, null));

        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal("SUBMITTED", item.Status);
        Assert.NotNull(item.Result);
        Assert.Equal(7, item.Result.Score);
        Assert.Equal("Leve", item.Result.InterpretationLabel);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldIsolateAttemptsByOwner()
    {
        var otherUserId = Guid.NewGuid();
        ArrangeAttempt(Now, AssessmentAttemptStatus.Submitted, userId: otherUserId);
        var myAttempt = ArrangeAttempt(Now.AddMinutes(-5), AssessmentAttemptStatus.Submitted);

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new ListAssessmentsQuery(null, null));

        Assert.Single(result.Items);
        Assert.Equal(myAttempt.AttemptId, result.Items[0].AttemptId);
    }

    [Fact]
    public async Task ExecuteAsync_WithKeysetPagination_ShouldNavigatePagesDeterministically()
    {
        var attempts = new List<AssessmentAttempt>();
        for (var i = 0; i < 5; i++)
        {
            attempts.Add(ArrangeAttempt(Now.AddMinutes(-i * 10)));
        }

        var useCase = CreateUseCase();

        // Página 1 (limit = 2)
        var page1 = await useCase.ExecuteAsync(new ListAssessmentsQuery(2, null));
        Assert.Equal(2, page1.Items.Count);
        Assert.NotNull(page1.NextCursor);
        Assert.Equal(attempts[0].AttemptId, page1.Items[0].AttemptId);
        Assert.Equal(attempts[1].AttemptId, page1.Items[1].AttemptId);

        // Página 2 (limit = 2 con cursor)
        var page2 = await useCase.ExecuteAsync(new ListAssessmentsQuery(2, page1.NextCursor));
        Assert.Equal(2, page2.Items.Count);
        Assert.NotNull(page2.NextCursor);
        Assert.Equal(attempts[2].AttemptId, page2.Items[0].AttemptId);
        Assert.Equal(attempts[3].AttemptId, page2.Items[1].AttemptId);

        // Página 3 (limit = 2 con cursor)
        var page3 = await useCase.ExecuteAsync(new ListAssessmentsQuery(2, page2.NextCursor));
        Assert.Single(page3.Items);
        Assert.Null(page3.NextCursor);
        Assert.Equal(attempts[4].AttemptId, page3.Items[0].AttemptId);
    }
}
