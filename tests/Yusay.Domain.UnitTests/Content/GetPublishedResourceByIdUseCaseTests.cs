using Yusay.Application.Common.Exceptions;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Queries.GetPublishedResourceById;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.UnitTests.Identity.Fakes;

namespace Yusay.Domain.UnitTests.Content;

public class GetPublishedResourceByIdUseCaseTests
{
    private readonly FakeCurrentUser _currentUser = new(Guid.NewGuid());
    private readonly FakeResourceCatalogRepository _repository = new();

    private GetPublishedResourceByIdUseCase CreateUseCase() => new(_currentUser, _repository);

    [Fact]
    public async Task ExecuteAsync_WithoutAuthenticatedUser_ShouldThrowUnauthorized()
    {
        _currentUser.UserId = null;
        var useCase = CreateUseCase();

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExecuteAsync_WhenResourceNotFound_ShouldThrowNotFound()
    {
        var useCase = CreateUseCase();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(Guid.NewGuid()));

        Assert.Equal("El recurso solicitado no existe o no se encuentra disponible.", exception.Message);
    }

    [Fact]
    public async Task ExecuteAsync_WhenResourceFound_ShouldReturnDetail()
    {
        var resourceId = Guid.NewGuid();
        var expected = new ResourceDetailsDto(
            resourceId,
            "ARTICLE",
            "PUBLISHED",
            "Guía de Respiración",
            "Resumen",
            "Cuerpo completo del artículo",
            null,
            new[] { new ResourceTopicDto(Guid.NewGuid(), "ansiedad", "Ansiedad") });

        _repository.ResourcesById[resourceId] = expected;
        var useCase = CreateUseCase();

        var result = await useCase.ExecuteAsync(resourceId);

        Assert.Equal(expected.ResourceId, result.ResourceId);
        Assert.Equal(expected.Title, result.Title);
        Assert.Equal(expected.Body, result.Body);
        Assert.Single(result.Topics);
    }

    private sealed class FakeResourceCatalogRepository : IResourceCatalogRepository
    {
        public Dictionary<Guid, ResourceDetailsDto> ResourcesById { get; } = new();

        public Task<ResourceDetailsDto?> GetPublishedByIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
        {
            ResourcesById.TryGetValue(resourceId, out var item);
            return Task.FromResult(item);
        }

        public Task<IReadOnlyList<ResourceSummaryDto>> ListPublishedAsync(
            Guid? topicId,
            Guid? dimensionId,
            Guid? interpretationId,
            string? type,
            int limit,
            string? cursorTitle,
            Guid? cursorResourceId,
            CancellationToken cancellationToken = default,
            System.Data.IDbTransaction? transaction = null) =>
            Task.FromResult<IReadOnlyList<ResourceSummaryDto>>(Array.Empty<ResourceSummaryDto>());

        public Task<Guid?> GetInterpretationIdForSubmittedAttemptAsync(
            Guid attemptId,
            Guid userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<Guid?>(null);

        public Task<IReadOnlyList<ResourceTopicDto>> GetInterpretationTopicsAsync(
            Guid interpretationId,
            System.Data.IDbTransaction? transaction = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResourceTopicDto>>(Array.Empty<ResourceTopicDto>());
    }

}
