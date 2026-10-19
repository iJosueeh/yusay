using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Application.Content.Commands.PublishResource;

public sealed class PublishResourceUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IResourceAdministrationRepository resourceRepository,
    IAuditEventRepository auditEventRepository) : IPublishResourceUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IResourceAdministrationRepository _resourceRepository = resourceRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<ResourceDetailsDto> ExecuteAsync(
        PublishResourceCommand command,
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

            if (resource.Status == ResourceStatus.Published)
            {
                return;
            }

            if (resource.Status == ResourceStatus.Retired)
            {
                throw new ConflictException("Transición inválida: un recurso en estado RETIRED no puede ser publicado.");
            }

            resource.Publish();

            await _resourceRepository.UpdateStatusAsync(resource.ResourceId, ResourceStatus.Published, tx, cancellationToken);

            var audit = AuditEvent.CreateResourcePublished(resource.ResourceId, adminUserId.Value);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        var details = await _resourceRepository.GetDetailsByIdAsync(command.ResourceId, cancellationToken);
        return details!;
    }
}
