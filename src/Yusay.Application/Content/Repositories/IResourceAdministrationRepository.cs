using System.Data.Common;
using Yusay.Application.Content.Dtos;
using Yusay.Domain.Content.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Application.Content.Repositories;

public interface IResourceAdministrationRepository
{
    Task<Resource?> GetByIdAsync(Guid resourceId, CancellationToken cancellationToken = default);

    Task<Resource?> GetByIdForUpdateAsync(
        Guid resourceId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<ResourceDetailsDto?> GetDetailsByIdAsync(Guid resourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ResourceSummaryDto>> ListAsync(
        string? status = null,
        string? type = null,
        Guid? topicId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Resource resource, DbTransaction transaction, CancellationToken cancellationToken = default);

    Task UpdateDraftAsync(Resource resource, DbTransaction transaction, CancellationToken cancellationToken = default);

    Task UpdatePublishedCorrectionAsync(
        Resource resource,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task UpdateStatusAsync(
        Guid resourceId,
        ResourceStatus status,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task AddTopicAssociationAsync(
        Guid resourceId,
        Guid topicId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task RemoveTopicAssociationAsync(
        Guid resourceId,
        Guid topicId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<bool> HasTopicAssociationAsync(
        Guid resourceId,
        Guid topicId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);

    Task<int> GetTopicCountAsync(
        Guid resourceId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default);
}
