using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Content.Repositories;

public interface IResourceCatalogRepository
{
    Task<ResourceDetailsDto?> GetPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceSummaryDto>> ListPublishedAsync(
        Guid? topicId,
        Guid? dimensionId,
        Guid? interpretationId,
        string? type,
        int limit,
        string? cursorTitle,
        Guid? cursorResourceId,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetInterpretationIdForSubmittedAttemptAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
