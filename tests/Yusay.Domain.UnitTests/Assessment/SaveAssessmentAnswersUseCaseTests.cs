using Yusay.Application.Assessment.Commands.SaveAssessmentAnswers;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public sealed class SaveAssessmentAnswersUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeAssessmentAttemptRepository _attemptRepository = new();
    private readonly FixedTimeProvider _timeProvider = new(DateTimeOffset.UtcNow);

    private SaveAssessmentAnswersUseCase CreateUseCase() =>
        new(_currentUser, _unitOfWork, _attemptRepository, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenUserNotAuthenticated_ThrowsUnauthorizedException()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                Guid.NewGuid(),
                new List<AssessmentAnswerItemDto> { new(Guid.NewGuid(), Guid.NewGuid()) })));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptIdEmpty_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                Guid.Empty,
                new List<AssessmentAnswerItemDto> { new(Guid.NewGuid(), Guid.NewGuid()) })));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnswersListEmpty_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                Guid.NewGuid(),
                new List<AssessmentAnswerItemDto>())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnswerHasEmptyQuestionOrOptionId_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                Guid.NewGuid(),
                new List<AssessmentAnswerItemDto> { new(Guid.Empty, Guid.NewGuid()) })));

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                Guid.NewGuid(),
                new List<AssessmentAnswerItemDto> { new(Guid.NewGuid(), Guid.Empty) })));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnswersContainDuplicateQuestionId_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();
        var qId = Guid.NewGuid();

        var command = new SaveAssessmentAnswersCommand(
            Guid.NewGuid(),
            new List<AssessmentAnswerItemDto>
            {
                new(qId, Guid.NewGuid()),
                new(qId, Guid.NewGuid())
            });

        var ex = await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
        Assert.Contains("No se permiten múltiples respuestas", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptNotFound_ThrowsNotFoundException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                Guid.NewGuid(),
                new List<AssessmentAnswerItemDto> { new(Guid.NewGuid(), Guid.NewGuid()) })));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptIsTerminal_ThrowsConflictException()
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

        var useCase = CreateUseCase();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                attempt.AttemptId,
                new List<AssessmentAnswerItemDto> { new(Guid.NewGuid(), Guid.NewGuid()) })));

        Assert.Equal("El intento de evaluación ya no se encuentra en curso.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptIsExpired_PersistsExpirationAndThrowsConflictException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        // Venció hace 10 horas
        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-730));
        _attemptRepository.Attempts.Add(attempt);

        var qId = Guid.NewGuid();
        var optId = Guid.NewGuid();
        _attemptRepository.SavedAnswers[attempt.AttemptId] = new Dictionary<Guid, Guid>
        {
            [qId] = optId
        };

        var useCase = CreateUseCase();

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                attempt.AttemptId,
                new List<AssessmentAnswerItemDto> { new(qId, optId) })));

        Assert.Equal("El intento de evaluación ha expirado y no admite más respuestas.", ex.Message);
        Assert.Contains(attempt.AttemptId, _attemptRepository.ExpiredAttempts);
        Assert.False(_attemptRepository.SavedAnswers.ContainsKey(attempt.AttemptId));
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuestionDoesNotBelongToVersion_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var validQuestion = Guid.NewGuid();
        var validOption = Guid.NewGuid();
        _attemptRepository.VersionAllowedOptions[versionId] = new Dictionary<Guid, HashSet<Guid>>
        {
            [validQuestion] = new() { validOption }
        };

        var useCase = CreateUseCase();

        var foreignQuestion = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                attempt.AttemptId,
                new List<AssessmentAnswerItemDto> { new(foreignQuestion, validOption) })));

        Assert.Contains("no pertenece a la versión", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenOptionDoesNotBelongToQuestion_ThrowsValidationException()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var validQuestion = Guid.NewGuid();
        var validOption = Guid.NewGuid();
        _attemptRepository.VersionAllowedOptions[versionId] = new Dictionary<Guid, HashSet<Guid>>
        {
            [validQuestion] = new() { validOption }
        };

        var useCase = CreateUseCase();

        var foreignOption = Guid.NewGuid();
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
                attempt.AttemptId,
                new List<AssessmentAnswerItemDto> { new(validQuestion, foreignOption) })));

        Assert.Contains("no es válida para la pregunta indicada", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenAnswersValid_SavesAnswersAndReturnsSavedCount()
    {
        var userId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, _timeProvider.GetUtcNow().AddHours(-1));
        _attemptRepository.Attempts.Add(attempt);

        var q1 = Guid.NewGuid();
        var q1Opt1 = Guid.NewGuid();
        var q1Opt2 = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var q2Opt1 = Guid.NewGuid();

        _attemptRepository.VersionAllowedOptions[versionId] = new Dictionary<Guid, HashSet<Guid>>
        {
            [q1] = new() { q1Opt1, q1Opt2 },
            [q2] = new() { q2Opt1 }
        };

        var useCase = CreateUseCase();

        // 1. Guardar respuesta inicial a q1
        var result1 = await useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
            attempt.AttemptId,
            new List<AssessmentAnswerItemDto> { new(q1, q1Opt1) }));

        Assert.Equal(attempt.AttemptId, result1.AttemptId);
        Assert.Equal(1, result1.SavedCount);
        Assert.Equal(q1Opt1, _attemptRepository.SavedAnswers[attempt.AttemptId][q1]);

        // 2. Actualizar respuesta de q1 a q1Opt2 y agregar q2 (UPSERT)
        var result2 = await useCase.ExecuteAsync(new SaveAssessmentAnswersCommand(
            attempt.AttemptId,
            new List<AssessmentAnswerItemDto>
            {
                new(q1, q1Opt2),
                new(q2, q2Opt1)
            }));

        Assert.Equal(attempt.AttemptId, result2.AttemptId);
        Assert.Equal(2, result2.SavedCount);
        Assert.Equal(q1Opt2, _attemptRepository.SavedAnswers[attempt.AttemptId][q1]);
        Assert.Equal(q2Opt1, _attemptRepository.SavedAnswers[attempt.AttemptId][q2]);
    }
}
