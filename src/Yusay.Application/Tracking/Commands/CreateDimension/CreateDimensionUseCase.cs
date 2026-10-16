using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Application.Tracking.Commands.CreateDimension;

// B5a de RF-021: alta administrativa de una Dimension en POST /admin/dimensions. La
// validación de forma (DimensionCode y Dimension) ocurre en dominio ANTES de abrir la
// transacción y se traduce en 400. Dentro de la transacción: pre-check de code duplicado
// (409 con el mensaje único del contrato), inserción y UN audit_event CATALOG_CREATED
// (actor ADMINISTRATOR, target DIMENSION, metadata NULL — perfil N de V011) en la misma
// transacción, de modo que un fallo de auditoría revierte el alta completa. La carrera
// entre el pre-check y la inserción la resuelve la traducción de 23505 (uq_dimension_code)
// a ConflictException con el mismo mensaje, acotada en el repositorio.
public sealed class CreateDimensionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IDimensionAdministrationRepository dimensionAdministrationRepository,
    IAuditEventRepository auditEventRepository) : ICreateDimensionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDimensionAdministrationRepository _dimensionAdministrationRepository =
        dimensionAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<CreateDimensionResult> ExecuteAsync(
        CreateDimensionCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Dimensions.");
        }

        var code = DimensionCode.Create(command.Code);
        var dimension = Dimension.Create(code, command.Name, command.Description);

        var audit = AuditEvent.CreateCatalogCreated(dimension.DimensionId, userId.Value);

        await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var codeExists = await _dimensionAdministrationRepository.CodeExistsAsync(
                code.Value, tx, cancellationToken);
            if (codeExists)
            {
                throw new ConflictException(IDimensionAdministrationRepository.DuplicateCodeConflictMessage);
            }

            await _dimensionAdministrationRepository.AddAsync(dimension, tx, cancellationToken);
            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);
        }, cancellationToken);

        return new CreateDimensionResult(
            dimension.DimensionId,
            dimension.Code.Value,
            dimension.Name,
            dimension.Description);
    }
}
