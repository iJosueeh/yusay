using Yusay.Application.Assessment.Commands.StartAssessmentAttempt;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Repositories;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public sealed class StartAssessmentAttemptUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAssessmentAttemptRepository _attemptRepository = new();
    private readonly FixedTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private StartAssessmentAttemptUseCase CreateUseCase() =>
        new(_currentUser, _unitOfWork, _attemptRepository, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenUserNotAuthenticated_ThrowsUnauthorizedException()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(new StartAssessmentAttemptCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenInstrumentIdEmpty_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new StartAssessmentAttemptCommand(Guid.Empty)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPublishedVersionNotFound_ThrowsNotFoundException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        _currentUser.UserId = userId;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new StartAssessmentAttemptCommand(instrumentId)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveAttemptExistsAndNotExpired_ThrowsConflictException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        _attemptRepository.PublishedVersions[instrumentId] = new PublishedVersionHeaderDto(versionId, 1);

        var existingAttempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(existingAttempt);

        var useCase = CreateUseCase();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new StartAssessmentAttemptCommand(instrumentId)));

        Assert.Equal(IAssessmentAttemptRepository.ActiveAttemptConflictMessage, ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveAttemptExistsAndIsExpired_AppliesLazyExpirationAndCreatesNewAttempt()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        _attemptRepository.PublishedVersions[instrumentId] = new PublishedVersionHeaderDto(versionId, 1);

        var oldStartTime = _timeProvider.GetUtcNow().AddHours(-750); // Started 750h ago, expired 30h ago
        var oldAttempt = AssessmentAttempt.Create(userId, instrumentId, versionId, oldStartTime);
        _attemptRepository.Attempts.Add(oldAttempt);

        var questionId = Guid.NewGuid();
        var optionId = Guid.NewGuid();
        _attemptRepository.VersionQuestions[versionId] = new List<AttemptQuestionDto>
        {
            new(questionId, 1, "Pregunta 1", true, new List<AttemptOptionDto>
            {
                new(optionId, 1, "Opción A")
            })
        };

        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new StartAssessmentAttemptCommand(instrumentId));

        Assert.NotNull(result);
        Assert.NotEqual(oldAttempt.AttemptId, result.AttemptId);
        Assert.Equal("IN_PROGRESS", result.Status);
        Assert.Single(result.Questions);

        // Old attempt must be expired
        Assert.Contains(oldAttempt.AttemptId, _attemptRepository.ExpiredAttempts);
        Assert.Equal(AssessmentAttemptStatus.Expired, oldAttempt.Status);
        Assert.Equal(oldAttempt.ExpiresAt, oldAttempt.EndedAt);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_AcquiresAdvisoryLockAndReturnsQuestionsWithoutScoring()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        _attemptRepository.PublishedVersions[instrumentId] = new PublishedVersionHeaderDto(versionId, 2);

        var q1 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        _attemptRepository.VersionQuestions[versionId] = new List<AttemptQuestionDto>
        {
            new(q1, 1, "Prompt 1", true, new List<AttemptOptionDto>
            {
                new(Guid.NewGuid(), 1, "O1"),
                new(Guid.NewGuid(), 2, "O2")
            }),
            new(q2, 2, "Prompt 2", false, new List<AttemptOptionDto>
            {
                new(Guid.NewGuid(), 1, "O3")
            })
        };

        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(new StartAssessmentAttemptCommand(instrumentId));

        Assert.NotNull(result);
        Assert.Equal(instrumentId, result.InstrumentId);
        Assert.Equal(versionId, result.InstrumentVersionId);
        Assert.Equal(2, result.Version);
        Assert.Equal("IN_PROGRESS", result.Status);
        Assert.Equal(2, result.Questions.Count);

        // Verify advisory lock key
        long expectedKey = StartAssessmentAttemptUseCase.ComputeAdvisoryLockKey(userId, instrumentId);
        Assert.Contains(expectedKey, _attemptRepository.AcquiredLockKeys);
    }

    [Fact]
    public void ComputeAdvisoryLockKey_IsDeterministicAndDifferentForDifferentInputs()
    {
        var u1 = Guid.NewGuid();
        var u2 = Guid.NewGuid();
        var i1 = Guid.NewGuid();

        long key1 = StartAssessmentAttemptUseCase.ComputeAdvisoryLockKey(u1, i1);
        long key1Again = StartAssessmentAttemptUseCase.ComputeAdvisoryLockKey(u1, i1);
        long key2 = StartAssessmentAttemptUseCase.ComputeAdvisoryLockKey(u2, i1);

        Assert.Equal(key1, key1Again);
        Assert.NotEqual(key1, key2);
    }
}
