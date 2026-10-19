using Yusay.Application.Common.Exceptions;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Queries.ListPublishedResources;
using Yusay.Application.Content.Queries.ListResources;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Content;

public class ListPublishedResourcesUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new(Guid.NewGuid());
    private readonly FakeResourceCatalogRepository _repository = new();

    private ListPublishedResourcesUseCase CreateUseCase() => new(_currentUser, _repository);

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task ExecuteAsync_WithInvalidLimit_ShouldThrowValidationException(int limit)
    {
        var useCase = CreateUseCase();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery(Limit: limit)));

        Assert.Equal("El parámetro limit debe ser un entero entre 1 y 100.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithMultipleContexts_ShouldThrowValidationException()
    {
        var useCase = CreateUseCase();
        var topicId = Guid.NewGuid();
        var dimensionId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        var expectedMsg = "Solo se permite especificar un criterio de contexto por solicitud: topicId, dimensionId o attemptId.";

        var ex1 = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery(TopicId: topicId, DimensionId: dimensionId)));
        Assert.Equal(expectedMsg, ex1.Message);

        var ex2 = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery(TopicId: topicId, AttemptId: attemptId)));
        Assert.Equal(expectedMsg, ex2.Message);

        var ex3 = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery(DimensionId: dimensionId, AttemptId: attemptId)));
        Assert.Equal(expectedMsg, ex3.Message);

        var ex4 = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery(TopicId: topicId, DimensionId: dimensionId, AttemptId: attemptId)));
        Assert.Equal(expectedMsg, ex4.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidType_ShouldThrowValidationException()
    {
        var useCase = CreateUseCase();

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery(Type: "INVALID")));

        Assert.Contains("El parámetro type 'INVALID' no es válido", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithAttemptId_WhenAttemptNotFoundOrNotSubmitted_ShouldThrowNotFound()
    {
        var useCase = CreateUseCase();
        var attemptId = Guid.NewGuid();

        // Repository returns null for interpretation_id (not found, different user, or not SUBMITTED)
        var ex = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new ListPublishedResourcesQuery(AttemptId: attemptId)));

        Assert.Equal("El intento especificado no existe o no cuenta con un resultado oficial disponible.", ex.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WithAttemptId_WhenAttemptFound_ShouldQueryWithResolvedInterpretationId()
    {
        var useCase = CreateUseCase();
        var attemptId = Guid.NewGuid();
        var expectedInterpretationId = Guid.NewGuid();

        _repository.SubmittedAttemptInterpretations[attemptId] = expectedInterpretationId;

        var result = await useCase.ExecuteAsync(new ListPublishedResourcesQuery(AttemptId: attemptId));

        Assert.NotNull(result);
        Assert.Equal(expectedInterpretationId, _repository.LastQueriedInterpretationId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMoreItemsExist_ShouldEncodeNextCursor()
    {
        var useCase = CreateUseCase();
        var resource1 = new ResourceSummaryDto(
            Guid.NewGuid(), "ARTICLE", "PUBLISHED", "Title 1", "Summary 1", 1, new[] { "T1" });
        var resource2 = new ResourceSummaryDto(
            Guid.NewGuid(), "ARTICLE", "PUBLISHED", "Title 2", "Summary 2", 1, new[] { "T2" });
        var resource3 = new ResourceSummaryDto(
            Guid.NewGuid(), "ARTICLE", "PUBLISHED", "Title 3", "Summary 3", 1, new[] { "T3" });

        // Limit = 2, repo returns 3 items (limit + 1)
        _repository.ItemsToReturn = new List<ResourceSummaryDto> { resource1, resource2, resource3 };

        var result = await useCase.ExecuteAsync(new ListPublishedResourcesQuery(Limit: 2));

        Assert.Equal(2, result.Items.Count);
        Assert.NotNull(result.NextCursor);
        var decoded = ResourceListCursor.Decode(result.NextCursor);
        Assert.NotNull(decoded);
        Assert.Equal(resource2.Title, decoded.Title);
        Assert.Equal(resource2.ResourceId, decoded.ResourceId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMoreItems_ShouldReturnNullNextCursor()
    {
        var useCase = CreateUseCase();
        var resource1 = new ResourceSummaryDto(
            Guid.NewGuid(), "ARTICLE", "PUBLISHED", "Title 1", "Summary 1", 1, new[] { "T1" });

        // Limit = 10, repo returns 1 item
        _repository.ItemsToReturn = new List<ResourceSummaryDto> { resource1 };

        var result = await useCase.ExecuteAsync(new ListPublishedResourcesQuery(Limit: 10));

        Assert.Single(result.Items);
        Assert.Null(result.NextCursor);
    }

    private sealed class FakeResourceCatalogRepository : IResourceCatalogRepository
    {
        public Dictionary<Guid, Guid> SubmittedAttemptInterpretations { get; } = new();
        public List<ResourceSummaryDto> ItemsToReturn { get; set; } = new();
        public Guid? LastQueriedInterpretationId { get; private set; }

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
            System.Data.IDbTransaction? transaction = null)
        {
            LastQueriedInterpretationId = interpretationId;
            return Task.FromResult<IReadOnlyList<ResourceSummaryDto>>(ItemsToReturn);
        }

        public Task<Guid?> GetInterpretationIdForSubmittedAttemptAsync(
            Guid attemptId,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            SubmittedAttemptInterpretations.TryGetValue(attemptId, out var interpretationId);
            return Task.FromResult(interpretationId == Guid.Empty ? (Guid?)null : interpretationId);
        }

        public Task<IReadOnlyList<ResourceTopicDto>> GetInterpretationTopicsAsync(
            Guid interpretationId,
            System.Data.IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceTopicDto>>(Array.Empty<ResourceTopicDto>());
    }
}
