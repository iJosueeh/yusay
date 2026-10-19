using System.Data;
using Yusay.Application.Assessment.Common;
using Yusay.Application.Assessment.Dtos;
using Yusay.Application.Assessment.Queries.GetAssessmentGuidance;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Assessment.Entities;
using Yusay.Domain.UnitTests.Assessment.Fakes;
using Yusay.Domain.UnitTests.Fakes;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Assessment;

public sealed class GetAssessmentGuidanceUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new();
    private readonly FakeAssessmentAttemptRepository _attemptRepository = new();
    private readonly FakeResourceCatalogRepository _resourceCatalogRepository = new();
    private readonly FakeUnitOfWork _unitOfWork = new();

    private GetAssessmentGuidanceUseCase CreateUseCase() =>
        new(_currentUser, _attemptRepository, _resourceCatalogRepository, _unitOfWork);

    [Fact]
    public async Task ExecuteAsync_WhenUserNotAuthenticated_ThrowsUnauthorizedException()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(new GetAssessmentGuidanceQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptIdEmpty_ThrowsValidationException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new GetAssessmentGuidanceQuery(Guid.Empty)));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptNotFoundOrNotSubmitted_ThrowsNotFoundException()
    {
        _currentUser.UserId = Guid.NewGuid();
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new GetAssessmentGuidanceQuery(Guid.NewGuid())));
    }

    [Fact]
    public async Task ExecuteAsync_WhenAttemptIsValidAndSubmitted_ReturnsGuidanceWithAllComponents()
    {
        var userId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var interpretationId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, DateTimeOffset.UtcNow.AddHours(-1));
        attempt.Submit(DateTimeOffset.UtcNow);
        typeof(AssessmentAttempt).GetProperty(nameof(AssessmentAttempt.AttemptId))!.SetValue(attempt, attemptId);
        _attemptRepository.Attempts.Add(attempt);
        _attemptRepository.AttemptResults[attemptId] = (14, interpretationId, DateTimeOffset.UtcNow);
        _attemptRepository.VersionInterpretations[versionId] = new List<InterpretationDataDto>
        {
            new(interpretationId, "Moderado", "Descripción moderada", "Limitación psicométrica", 10, 15)
        };

        var topicId = Guid.NewGuid();
        _resourceCatalogRepository.TopicsByInterpretation[interpretationId] = new List<ResourceTopicDto>
        {
            new(topicId, "ANXIETY", "Ansiedad")
        };

        _resourceCatalogRepository.ResourcesByInterpretation[interpretationId] = new List<ResourceSummaryDto>
        {
            new(Guid.NewGuid(), "ARTICLE", "PUBLISHED", "Guía de autocuidado", "Resumen", 1, new[] { "ANXIETY" })
        };

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetAssessmentGuidanceQuery(attemptId));

        Assert.NotNull(result);
        Assert.Equal(attemptId, result.AttemptId);
        Assert.Equal(instrumentId, result.Instrument.InstrumentId);
        Assert.Equal("TEST-CODE", result.Instrument.Code);
        Assert.Equal(14, result.Result.Score);
        Assert.Equal(interpretationId, result.Interpretation.InterpretationId);
        Assert.Equal("Moderado", result.Interpretation.Label);
        Assert.Equal("Descripción moderada", result.Interpretation.Description);
        Assert.Equal("Limitación psicométrica", result.Interpretation.Limitations);
        Assert.Single(result.Topics);
        Assert.Equal("ANXIETY", result.Topics[0].Code);
        Assert.Single(result.Resources);
        Assert.Equal("Guía de autocuidado", result.Resources[0].Title);
        Assert.Equal(AssessmentGuidanceConstants.NonClinicalDisclaimer, result.Disclaimer);
        Assert.True(_unitOfWork.TransactionExecuted);
    }

    [Fact]
    public async Task ExecuteAsync_WhenInterpretationHasNoTopics_ReturnsGuidanceWithEmptyTopicsAndResources()
    {
        var userId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var interpretationId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, DateTimeOffset.UtcNow.AddHours(-1));
        attempt.Submit(DateTimeOffset.UtcNow);
        typeof(AssessmentAttempt).GetProperty(nameof(AssessmentAttempt.AttemptId))!.SetValue(attempt, attemptId);
        _attemptRepository.Attempts.Add(attempt);
        _attemptRepository.AttemptResults[attemptId] = (8, interpretationId, DateTimeOffset.UtcNow);

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetAssessmentGuidanceQuery(attemptId));

        Assert.NotNull(result);
        Assert.Empty(result.Topics);
        Assert.Empty(result.Resources);
        Assert.Equal(AssessmentGuidanceConstants.NonClinicalDisclaimer, result.Disclaimer);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTopicsHaveNoPublishedResources_ReturnsGuidanceWithTopicsAndEmptyResources()
    {
        var userId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();
        var instrumentId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var interpretationId = Guid.NewGuid();
        _currentUser.UserId = userId;

        var attempt = AssessmentAttempt.Create(userId, instrumentId, versionId, DateTimeOffset.UtcNow.AddHours(-1));
        attempt.Submit(DateTimeOffset.UtcNow);
        typeof(AssessmentAttempt).GetProperty(nameof(AssessmentAttempt.AttemptId))!.SetValue(attempt, attemptId);
        _attemptRepository.Attempts.Add(attempt);
        _attemptRepository.AttemptResults[attemptId] = (8, interpretationId, DateTimeOffset.UtcNow);

        _resourceCatalogRepository.TopicsByInterpretation[interpretationId] = new List<ResourceTopicDto>
        {
            new(Guid.NewGuid(), "WELLBEING", "Bienestar")
        };

        var useCase = CreateUseCase();
        var result = await useCase.ExecuteAsync(new GetAssessmentGuidanceQuery(attemptId));

        Assert.NotNull(result);
        Assert.Single(result.Topics);
        Assert.Empty(result.Resources);
        Assert.Equal(AssessmentGuidanceConstants.NonClinicalDisclaimer, result.Disclaimer);
    }

    private sealed class FakeResourceCatalogRepository : IResourceCatalogRepository
    {
        public Dictionary<Guid, List<ResourceTopicDto>> TopicsByInterpretation { get; } = new();
        public Dictionary<Guid, List<ResourceSummaryDto>> ResourcesByInterpretation { get; } = new();

        public Task<ResourceDetailsDto?> GetPublishedByIdAsync(Guid resourceId, CancellationToken cancellationToken = default) =>
            Task.FromResult<ResourceDetailsDto?>(null);

        public Task<IReadOnlyList<ResourceSummaryDto>> ListPublishedAsync(
            Guid? topicId,
            Guid? dimensionId,
            Guid? interpretationId,
            string? type,
            int limit,
            string? cursorTitle,
            Guid? cursorResourceId,
            CancellationToken cancellationToken = default,
            IDbTransaction? transaction = null)
        {
            if (interpretationId.HasValue && ResourcesByInterpretation.TryGetValue(interpretationId.Value, out var list))
            {
                return Task.FromResult<IReadOnlyList<ResourceSummaryDto>>(list.Take(limit).ToList());
            }

            return Task.FromResult<IReadOnlyList<ResourceSummaryDto>>(Array.Empty<ResourceSummaryDto>());
        }

        public Task<Guid?> GetInterpretationIdForSubmittedAttemptAsync(Guid attemptId, Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<IReadOnlyList<ResourceTopicDto>> GetInterpretationTopicsAsync(
            Guid interpretationId,
            IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            if (TopicsByInterpretation.TryGetValue(interpretationId, out var topics))
            {
                return Task.FromResult<IReadOnlyList<ResourceTopicDto>>(topics);
            }

            return Task.FromResult<IReadOnlyList<ResourceTopicDto>>(Array.Empty<ResourceTopicDto>());
        }
    }
}
