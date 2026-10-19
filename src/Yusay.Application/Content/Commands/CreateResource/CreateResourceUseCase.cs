using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Content.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Application.Content.Commands.CreateResource;

public sealed class CreateResourceUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IResourceAdministrationRepository resourceRepository,
    ITopicRepository topicRepository,
    IAuditEventRepository auditEventRepository) : ICreateResourceUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IResourceAdministrationRepository _resourceRepository = resourceRepository;
    private readonly ITopicRepository _topicRepository = topicRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<ResourceDetailsDto> ExecuteAsync(
        CreateResourceCommand command,
        CancellationToken cancellationToken = default)
    {
        var adminUserId = _currentUser.UserId;
        if (adminUserId is null || adminUserId == Guid.Empty)
        {
            throw new UnauthorizedException("Se requiere una identidad de usuario autenticada para administrar recursos.");
        }

        var resourceType = ResourceTypeExtensions.FromDbValue(command.Type);

        var distinctTopicIds = command.TopicIds?.Distinct().ToList() ?? [];
        if (distinctTopicIds.Count > 0)
        {
            var allTopicsExist = await _topicRepository.AllTopicsExistAsync(distinctTopicIds, cancellationToken);
            if (!allTopicsExist)
            {
                throw new NotFoundException("Uno o más temas especificados no existen.");
            }
        }

        var resource = Resource.CreateDraft(
            resourceType,
            command.Title,
            command.Summary,
            command.Body,
            command.ExternalUrl,
            distinctTopicIds);

        var auditEvents = new List<AuditEvent>
        {
            AuditEvent.CreateResourceCreated(resource.ResourceId, adminUserId.Value)
        };

        foreach (var topicId in distinctTopicIds)
        {
            auditEvents.Add(AuditEvent.CreateEditorialAssociationAdded(
                resource.ResourceId,
                topicId,
                adminUserId.Value));
        }

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            await _resourceRepository.AddAsync(resource, tx, cancellationToken);

            foreach (var topicId in distinctTopicIds)
            {
                await _resourceRepository.AddTopicAssociationAsync(resource.ResourceId, topicId, tx, cancellationToken);
            }

            foreach (var audit in auditEvents)
            {
                await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
            }
        }, cancellationToken);

        var details = await _resourceRepository.GetDetailsByIdAsync(resource.ResourceId, cancellationToken);
        return details!;
    }
}
