using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Application.Content.Commands.UpdateResource;

public sealed class UpdateResourceUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IResourceAdministrationRepository resourceRepository,
    IAuditEventRepository auditEventRepository) : IUpdateResourceUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IResourceAdministrationRepository _resourceRepository = resourceRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<ResourceDetailsDto> ExecuteAsync(
        UpdateResourceCommand command,
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
                throw new ConflictException("No se puede modificar un recurso en estado RETIRED.");
            }

            var changedFields = new List<string>();

            if (resource.Status == ResourceStatus.Draft)
            {
                var targetType = command.Type != null
                    ? ResourceTypeExtensions.FromDbValue(command.Type)
                    : resource.Type;

                if (resource.Title != command.Title.Trim()) changedFields.Add("title");
                if (resource.Summary != (string.IsNullOrWhiteSpace(command.Summary) ? null : command.Summary.Trim())) changedFields.Add("summary");
                if (resource.Body != (string.IsNullOrWhiteSpace(command.Body) ? null : command.Body.Trim())) changedFields.Add("body");
                if (resource.ExternalUrl != (string.IsNullOrWhiteSpace(command.ExternalUrl) ? null : command.ExternalUrl.Trim())) changedFields.Add("external_url");

                resource.UpdateDraft(targetType, command.Title, command.Summary, command.Body, command.ExternalUrl);
                await _resourceRepository.UpdateDraftAsync(resource, tx, cancellationToken);

                if (changedFields.Count > 0)
                {
                    var audit = AuditEvent.CreateResourceUpdated(
                        resource.ResourceId,
                        adminUserId.Value,
                        changedFields,
                        correctionKind: null);
                    await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
                }
            }
            else if (resource.Status == ResourceStatus.Published)
            {
                if (command.Type != null && ResourceTypeExtensions.FromDbValue(command.Type) != resource.Type)
                {
                    throw new ConflictException(
                        "El tipo (type) de un recurso es inmutable tras su publicación. Cambiar el tipo requiere crear un nuevo recurso.");
                }

                if (string.IsNullOrWhiteSpace(command.CorrectionKind))
                {
                    throw new ValidationException(
                        "Para modificar un recurso en estado PUBLISHED es obligatorio especificar el tipo de corrección editorial menor (correctionKind).");
                }

                var correctionKind = CorrectionKindExtensions.FromDbValue(command.CorrectionKind);

                if (resource.Title != command.Title.Trim()) changedFields.Add("title");
                if (resource.Summary != (string.IsNullOrWhiteSpace(command.Summary) ? null : command.Summary.Trim())) changedFields.Add("summary");
                if (resource.Body != (string.IsNullOrWhiteSpace(command.Body) ? null : command.Body.Trim())) changedFields.Add("body");
                if (resource.ExternalUrl != (string.IsNullOrWhiteSpace(command.ExternalUrl) ? null : command.ExternalUrl.Trim())) changedFields.Add("external_url");

                resource.ApplyMinorCorrection(
                    command.Title,
                    command.Summary,
                    command.Body,
                    command.ExternalUrl,
                    correctionKind);

                await _resourceRepository.UpdatePublishedCorrectionAsync(resource, tx, cancellationToken);

                if (changedFields.Count > 0)
                {
                    var audit = AuditEvent.CreateResourceUpdated(
                        resource.ResourceId,
                        adminUserId.Value,
                        changedFields,
                        correctionKind: correctionKind.ToDbValue());
                    await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
                }
            }
        }, cancellationToken);

        var details = await _resourceRepository.GetDetailsByIdAsync(command.ResourceId, cancellationToken);
        return details!;
    }
}
