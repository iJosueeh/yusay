using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Application.Tracking.Commands.UpdateDimension;

// B5a de RF-021: edición administrativa de nombre/descripción en PATCH
// /admin/dimensions/{dimensionId}. Toda la operación ocurre dentro de una transacción con
// SELECT FOR UPDATE sobre la fila de la Dimensión: la lectura bloqueada es el estado sobre
// el que se detectan los cambios efectivos y sobre el que se escribe (última escritura
// gana). Semántica PATCH: campo ausente o null conserva el valor almacenado; la comparación
// es canónica (trim), de modo que reenviar el mismo valor con distintos espacios no cuenta
// como cambio. Sin cambios efectivos -> 400 sin escribir ni auditar. code no participa: es
// inmutable tras el alta (B5a no lo transporta ni lo actualiza). Se registra UN
// audit_event CATALOG_UPDATED (actor ADMINISTRATOR, target DIMENSION, perfil C de V011) con
// changed_fields limitado a la lista cerrada name/description y solo con los campos que
// realmente cambiaron, en la misma transacción.
public sealed class UpdateDimensionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IDimensionAdministrationRepository dimensionAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IUpdateDimensionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDimensionAdministrationRepository _dimensionAdministrationRepository =
        dimensionAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<UpdateDimensionResult> ExecuteAsync(
        UpdateDimensionCommand command,
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
                command.DimensionId, tx, cancellationToken);
            if (dimension is null)
            {
                throw new NotFoundException("La Dimensión solicitada no existe.");
            }

            var previousName = dimension.Name;
            var previousDescription = dimension.Description;

            // UpdateDetails valida (no vacío) y normaliza con trim el estado candidato
            dimension.UpdateDetails(
                command.Name ?? dimension.Name,
                command.Description ?? dimension.Description);

            if (dimension.Name == previousName && dimension.Description == previousDescription)
            {
                throw new ValidationException(
                    "La solicitud no modifica ningún campo de la Dimensión: no hay cambios efectivos que aplicar.");
            }

            var changedFields = new List<string>(2);
            if (dimension.Name != previousName)
            {
                changedFields.Add("name");
            }

            if (dimension.Description != previousDescription)
            {
                changedFields.Add("description");
            }

            var audit = AuditEvent.CreateCatalogUpdated(
                dimension.DimensionId, userId.Value, changedFields);

            await _dimensionAdministrationRepository.UpdateDetailsAsync(dimension, tx, cancellationToken);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            return new UpdateDimensionResult(
                dimension.DimensionId,
                dimension.Code.Value,
                dimension.Name,
                dimension.Description);
        }, cancellationToken);
    }
}
