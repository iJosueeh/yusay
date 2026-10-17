using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.Dimensions;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Application.Tracking.Commands.CreateDimensionVersion;

public sealed class CreateDimensionVersionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IDimensionAdministrationRepository dimensionAdministrationRepository,
    IAuditEventRepository auditEventRepository) : ICreateDimensionVersionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDimensionAdministrationRepository _dimensionAdministrationRepository =
        dimensionAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<DimensionVersionResult> ExecuteAsync(
        CreateDimensionVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Dimensions.");
        }

        var scale = DimensionScale.Create(command.MinValue, command.MaxValue, command.Step);
        var anchors = command.Anchors?
            .Select(anchor => DimensionAnchor.Create(anchor.Value, anchor.Label))
            .ToArray();
        var version = DimensionVersion.CreateDraft(
            command.DimensionId, command.Version, scale, command.Definition, anchors);

        var audit = AuditEvent.CreateCatalogVersionCreated(version.DimensionVersionId, userId.Value);

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var dimension = await _dimensionAdministrationRepository.GetByIdForUpdateAsync(
                command.DimensionId, tx, cancellationToken);
            if (dimension is null)
            {
                throw new NotFoundException("La Dimensión solicitada no existe.");
            }

            var versionNumberExists = await _dimensionAdministrationRepository.VersionNumberExistsAsync(
                command.DimensionId, command.Version, tx, cancellationToken);
            if (versionNumberExists)
            {
                throw new ConflictException(
                    IDimensionAdministrationRepository.DuplicateVersionNumberConflictMessage);
            }

            await _dimensionAdministrationRepository.AddVersionAsync(version, tx, cancellationToken);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        return DimensionCatalogResultMapper.ToResult(version);
    }
}
