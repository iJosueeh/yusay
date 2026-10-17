using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.Dimensions;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Tracking.Enums;

namespace Yusay.Application.Tracking.Commands.RetireDimensionVersion;

public sealed class RetireDimensionVersionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IDimensionAdministrationRepository dimensionAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IRetireDimensionVersionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDimensionAdministrationRepository _dimensionAdministrationRepository =
        dimensionAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<DimensionVersionResult> ExecuteAsync(
        RetireDimensionVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Dimensions.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var dimension = await _dimensionAdministrationRepository.GetByIdForUpdateAsync(
                command.DimensionId, tx, cancellationToken) ?? throw new NotFoundException("La Dimensión solicitada no existe.");

            var version = await _dimensionAdministrationRepository.GetVersionForUpdateAsync(
                dimension.DimensionId, command.DimensionVersionId, tx, cancellationToken) ?? throw new NotFoundException("La versión de Dimensión solicitada no existe.");
            if (version.Status != DimensionVersionStatus.Active)
            {
                throw new ConflictException(
                    "La versión de la Dimensión solo puede retirarse mientras está en estado ACTIVE.");
            }

            version.Retire();

            var updated = await _dimensionAdministrationRepository.UpdateStatusAsync(
                version.DimensionVersionId, DimensionVersionStatus.Retired, tx, cancellationToken);
            if (!updated)
            {
                throw new InvalidOperationException(
                    $"La actualización de estado a RETIRED de la versión {version.DimensionVersionId} no afectó a ninguna fila.");
            }

            var audit = AuditEvent.CreateCatalogVersionRetired(
                version.DimensionVersionId, userId.Value);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            return DimensionCatalogResultMapper.ToResult(version);
        }, cancellationToken);
    }
}
