using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Application.Content.Commands.RemoveResourceTopic;

public sealed class RemoveResourceTopicUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IResourceAdministrationRepository resourceRepository,
    IAuditEventRepository auditEventRepository) : IRemoveResourceTopicUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IResourceAdministrationRepository _resourceRepository = resourceRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<ResourceDetailsDto> ExecuteAsync(
        RemoveResourceTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para administrar recursos.");
        }

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var resource = await _resourceRepository.GetByIdForUpdateAsync(command.ResourceId, tx, cancellationToken);
            if (resource is null)
            {
                throw new NotFoundException("El recurso especificado no existe.");
            }

            if (resource.Status == ResourceStatus.Retired)
            {
                throw new ConflictException("No se pueden desvincular temas de un recurso en estado RETIRED.");
            }

            var hasAssociation = await _resourceRepository.HasTopicAssociationAsync(
                command.ResourceId,
                command.TopicId,
                tx,
                cancellationToken);

            if (!hasAssociation)
            {
                return; // Idempotente
            }

            var topicCount = await _resourceRepository.GetTopicCountAsync(command.ResourceId, tx, cancellationToken);
            if (resource.Status == ResourceStatus.Published && topicCount <= 1)
            {
                throw new ConflictException(
                    "No se puede desvincular el único tema de un recurso publicado. Un recurso publicado debe mantener al menos un tema asociado.");
            }

            resource.RemoveTopic(command.TopicId);

            await _resourceRepository.RemoveTopicAssociationAsync(
                resource.ResourceId,
                command.TopicId,
                tx,
                cancellationToken);

            var audit = AuditEvent.CreateEditorialAssociationRemoved(
                resource.ResourceId,
                command.TopicId,
                adminUserId.Value);

            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        var details = await _resourceRepository.GetDetailsByIdAsync(command.ResourceId, cancellationToken);
        return details!;
    }
}
