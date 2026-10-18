using Yusay.Application.Assessment.Commands.SubmitAssessmentAttempt;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public sealed class SubmitAssessmentAttemptUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAssessmentAttemptRepository _attemptRepository = new();
    private readonly FixedTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private SubmitAssessmentAttemptUseCase CreateUseCase() =>
        new(_currentUser, _unitOfWork, _attemptRepository, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenUserNotAuthenticated_ThrowsUnauthorizedException()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptIdEmpty_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(Guid.Empty)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptNotFound_ThrowsNotFoundException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAlreadySubmitted_ReturnsExistingResultIdempotently()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Rehydrate(
            Guid.NewGuid(),
            userId,
            instrumentId,
            versionId,
            AssessmentAttemptStatus.Submitted,
            _timeProvider.GetUtcNow().AddHours(-2),
            _timeProvider.GetUtcNow().AddHours(718),
            _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var interpId = Guid.NewGuid();
        _attemptRepository.AttemptResults[attempt.AttemptId] = (
            Score: 10,
            InterpretationId: interpId,
            CalculatedAt: _timeProvider.GetUtcNow().AddHours(-1));

        _attemptRepository.VersionInterpretations[versionId] = new List<InterpretationDataDto>
        {
            new(interpId, "Leve", "Sintomatología leve", null, 0, 15)
        };

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(attempt.AttemptId));

        Assert.NotNull(result);
        Assert.Equal(attempt.AttemptId, result.AttemptId);
        Assert.Equal(10, result.Score);
        Assert.Equal("Leve", result.Interpretation.Label);
        Assert.Equal("SUBMITTED", result.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptCancelledOrExpired_ThrowsConflictException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Rehydrate(
            Guid.NewGuid(),
            userId,
            instrumentId,
            versionId,
            AssessmentAttemptStatus.Cancelled,
            _timeProvider.GetUtcNow().AddHours(-2),
            _timeProvider.GetUtcNow().AddHours(718),
            _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var useCase = CreateUseCase();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(attempt.AttemptId)));

        Assert.Equal("El intento de evaluación ya no se encuentra en curso.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptInProgressAndExpired_AppliesLazyExpirationAndThrowsConflictException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        // Venció hace 20 horas
        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-740));
        _attemptRepository.Attempts.Add(attempt);

        var q1 = Guid.NewGuid();
        var opt1 = Guid.NewGuid();
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid> { [q1] = opt1 };

        var useCase = CreateUseCase();
        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(attempt.AttemptId)));

        Assert.Equal("El intento de evaluación ha expirado y no admite más respuestas.", ex.Message);
        Assert.Contains(attempt.AttemptId, _attemptRepository.ExpiredAttempts);
        Assert.False(_attemptRepository.SavedAnswers.ContainsKey(attempt.AttemptId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenMissingRequiredQuestions_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var q1 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var opt1 = Guid.NewGuid();

        _attemptRepository.VersionRequiredQuestions[versionId] = new List<Guid> { q1, q2 };
        // Solo q1 fue respondida
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid> { [q1] = opt1 };

        var useCase = CreateUseCase();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(attempt.AttemptId)));

        Assert.Contains("faltan respuestas a preguntas obligatorias", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOptionLacksScoringContribution_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var q1 = Guid.NewGuid();
        var opt1 = Guid.NewGuid();

        _attemptRepository.VersionRequiredQuestions[versionId] = new List<Guid> { q1 };
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid> { [q1] = opt1 };
        // Sin contribución en VersionContributions
        _attemptRepository.VersionContributions[versionId] = new Dictionary<(Guid QuestionId, Guid OptionId), int>();

        var useCase = CreateUseCase();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(attempt.AttemptId)));

        Assert.Contains("carecen de contribución de scoring", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCompleteAndValid_CalculatesSumAndPersistsResult()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);
        _attemptRepository.VersionNumbers[versionId] = 1;

        var q1 = Guid.NewGuid();
        var opt1 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var opt2 = Guid.NewGuid();

        _attemptRepository.VersionRequiredQuestions[versionId] = new List<Guid> { q1, q2 };
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid>
        {
            [q1] = opt1,
            [q2] = opt2
        };

        _attemptRepository.VersionContributions[versionId] = new Dictionary<(Guid QuestionId, Guid OptionId), int>
        {
            [(q1, opt1)] = 2,
            [(q2, opt2)] = 3
        };

        var interpId = Guid.NewGuid();
        _attemptRepository.VersionInterpretations[versionId] = new List<InterpretationDataDto>
        {
            new(Guid.NewGuid(), "Mínima", "0-3", null, 0, 3),
            new(interpId, "Moderada", "4-7", "Aclaración clínica", 4, 7),
            new(Guid.NewGuid(), "Severa", "8-10", null, 8, 10)
        };

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(attempt.AttemptId));

        Assert.NotNull(result);
        Assert.Equal(attempt.AttemptId, result.AttemptId);
        Assert.Equal(5, result.Score); // 2 + 3 = 5
        Assert.Equal("SUBMITTED", result.Status);
        Assert.Equal("Moderada", result.Interpretation.Label);
        Assert.Equal("Aclaración clínica", result.Interpretation.Limitations);

        // Intento en estado Submitted en repositorio
        var updatedAttempt = _attemptRepository.Attempts.First(a => a.AttemptId == attempt.AttemptId);
        Assert.Equal(AssessmentAttemptStatus.Submitted, updatedAttempt.Status);
        Assert.NotNull(updatedAttempt.EndedAt);
    }

    [Theory]
    [InlineData(0, "Mínima")]
    [InlineData(3, "Mínima")] // límite superior de Mínima
    [InlineData(4, "Moderada")] // límite inferior de Moderada
    [InlineData(7, "Moderada")] // límite superior de Moderada
    public async Task ExecuteAsync_WhenScoreAtBounds_ResolvesExactInterpretation(int score, string expectedLabel)
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var q1 = Guid.NewGuid();
        var opt1 = Guid.NewGuid();

        _attemptRepository.VersionRequiredQuestions[versionId] = new List<Guid> { q1 };
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid> { [q1] = opt1 };
        _attemptRepository.VersionContributions[versionId] = new Dictionary<(Guid QuestionId, Guid OptionId), int>
        {
            [(q1, opt1)] = score
        };

        _attemptRepository.VersionInterpretations[versionId] = new List<InterpretationDataDto>
        {
            new(Guid.NewGuid(), "Mínima", "0-3", null, 0, 3),
            new(Guid.NewGuid(), "Moderada", "4-7", null, 4, 7)
        };

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new SubmitAssessmentAttemptCommand(attempt.AttemptId));

        Assert.Equal(score, result.Score);
        Assert.Equal(expectedLabel, result.Interpretation.Label);
    }
}
