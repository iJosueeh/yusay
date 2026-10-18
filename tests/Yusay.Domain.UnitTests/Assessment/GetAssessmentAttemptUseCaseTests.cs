using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Queries.GetAssessmentAttempt;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public sealed class GetAssessmentAttemptUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeAssessmentAttemptRepository _attemptRepository = new();
    private readonly FixedTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private GetAssessmentAttemptUseCase CreateUseCase() =>
        new(_currentUser, _attemptRepository, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenUserNotAuthenticated_ThrowsUnauthorizedException()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(new GetAssessmentAttemptQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptIdEmpty_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new GetAssessmentAttemptQuery(Guid.Empty)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptNotFound_ThrowsNotFoundException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new GetAssessmentAttemptQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptInProgressAndNotExpired_ReturnsQuestionsAndSelectedAnswers()
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
        var opt2 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var opt3 = Guid.NewGuid();

        _attemptRepository.VersionQuestions[versionId] = new List<AttemptQuestionDto>
        {
            new(q1, 1, "¿Cómo te sientes?", true, new List<AttemptOptionDto>
            {
                new(opt1, 1, "Bien"),
                new(opt2, 2, "Mal")
            }),
            new(q2, 2, "¿Tienes energía?", false, new List<AttemptOptionDto>
            {
                new(opt3, 1, "Sí")
            })
        };

        // Simular respuesta previa a q1
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid>
        {
            [q1] = opt1
        };

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetAssessmentAttemptQuery(attempt.AttemptId));

        Assert.NotNull(result);
        Assert.Equal(attempt.AttemptId, result.AttemptId);
        Assert.Equal(instrumentId, result.InstrumentId);
        Assert.Equal(versionId, result.InstrumentVersionId);
        Assert.Equal(1, result.Version);
        Assert.Equal("IN_PROGRESS", result.Status);
        Assert.Equal(2, result.Questions.Count);

        var firstQuestion = result.Questions.First(q => q.QuestionId == q1);
        Assert.Equal(opt1, firstQuestion.SelectedOptionId);

        var secondQuestion = result.Questions.First(q => q.QuestionId == q2);
        Assert.Null(secondQuestion.SelectedOptionId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptInProgressAndExpired_AppliesLazyExpirationAndReturnsExpiredStatusWithNoAnswers()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        // Inició hace 750 horas (venció hace 30 horas)
        var startedAt = _timeProvider.GetUtcNow().AddHours(-750);
        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, startedAt);
        _attemptRepository.Attempts.Add(attempt);
        _attemptRepository.VersionNumbers[versionId] = 1;

        var q1 = Guid.NewGuid();
        var opt1 = Guid.NewGuid();
        _attemptRepository.VersionQuestions[versionId] = new List<AttemptQuestionDto>
        {
            new(q1, 1, "Pregunta", true, new List<AttemptOptionDto> { new(opt1, 1, "Opción") })
        };
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid>
        {
            [q1] = opt1
        };

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetAssessmentAttemptQuery(attempt.AttemptId));

        Assert.NotNull(result);
        Assert.Equal("EXPIRED", result.Status);
        Assert.Contains(attempt.AttemptId, _attemptRepository.ExpiredAttempts);

        // Respuestas borradas por la expiración
        Assert.Single(result.Questions);
        Assert.Null(result.Questions[0].SelectedOptionId);
    }
}
