using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Application.Content.Commands.AddResourceTopic;

public sealed class AddResourceTopicUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IResourceAdministrationRepository resourceRepository,
    ITopicRepository topicRepository,
    IAuditEventRepository auditEventRepository) : IAddResourceTopicUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IResourceAdministrationRepository _resourceRepository = resourceRepository;
    private readonly ITopicRepository _topicRepository = topicRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<ResourceDetailsDto> ExecuteAsync(
        AddResourceTopicCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para administrar recursos.");
        }

        var topic = await _topicRepository.GetByIdAsync(command.TopicId, cancellationToken);
        if (topic is null)
        {
            throw new NotFoundException("El tema especificado no existe.");
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
                throw new ConflictException("No se pueden asociar temas a un recurso en estado RETIRED.");
            }

            var alreadyAssociated = await _resourceRepository.HasTopicAssociationAsync(
                command.ResourceId,
                command.TopicId,
                tx,
                cancellationToken);

            if (alreadyAssociated)
            {
                return; // Idempotente
            }

            resource.AddTopic(command.TopicId);

            await _resourceRepository.AddTopicAssociationAsync(
                resource.ResourceId,
                command.TopicId,
                tx,
                cancellationToken);

            var audit = AuditEvent.CreateEditorialAssociationAdded(
                resource.ResourceId,
                command.TopicId,
                adminUserId.Value);

            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        var details = await _resourceRepository.GetDetailsByIdAsync(command.ResourceId, cancellationToken);
        return details!;
    }
}
