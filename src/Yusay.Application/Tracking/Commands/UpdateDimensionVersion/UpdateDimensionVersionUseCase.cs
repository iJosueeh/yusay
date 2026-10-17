using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Tracking.Queries.Dimensions;
using Yusay.Application.Tracking.Repositories;
using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Application.Tracking.Commands.UpdateDimensionVersion;

public sealed class UpdateDimensionVersionUseCase(
    ICurrentUser currentUser,
    IUnitOfWork unitOfWork,
    IDimensionAdministrationRepository dimensionAdministrationRepository,
    IAuditEventRepository auditEventRepository) : IUpdateDimensionVersionUseCase
{
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDimensionAdministrationRepository _dimensionAdministrationRepository =
        dimensionAdministrationRepository;
    private readonly IAuditEventRepository _auditEventRepository = auditEventRepository;

    public async Task<DimensionVersionResult> ExecuteAsync(
        UpdateDimensionVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        if (userId is null || userId == Guid.Empty)
        {
            throw new UnauthorizedException(
                "Se requiere una identidad de usuario autenticada para administrar el catálogo de Dimensions.");
        }

        var scaleProvided = command.MinValue.HasValue ||
            command.MaxValue.HasValue || command.Step.HasValue;
        if (scaleProvided &&
            (!command.MinValue.HasValue || !command.MaxValue.HasValue || !command.Step.HasValue))
        {
            throw new ValidationException(
                "La escala es un valor atómico: minValue, maxValue y step deben proporcionarse juntos.");
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async tx =>
        {
            var dimension = await _dimensionAdministrationRepository.GetByIdForUpdateAsync(
                command.DimensionId, tx, cancellationToken) ?? throw new NotFoundException("La Dimensión solicitada no existe.");

            var version = await _dimensionAdministrationRepository.GetVersionForUpdateAsync(
                command.DimensionId, command.DimensionVersionId, tx, cancellationToken) ?? throw new NotFoundException("La versión de Dimensión solicitada no existe.");
            if (version.Status != DimensionVersionStatus.Draft)
            {
                throw new ConflictException(
                    "La versión de la Dimensión solo puede editarse mientras está en estado DRAFT.");
            }

            var finalScale = scaleProvided
                ? DimensionScale.Create(
                    command.MinValue!.Value, command.MaxValue!.Value, command.Step!.Value)
                : version.Scale;

            IReadOnlyList<DimensionAnchor> finalAnchors;
            if (command.Anchors is null)
            {
                finalAnchors = version.Anchors;
                foreach (var anchor in finalAnchors)
                {
                    finalScale.EnsureWithinScale(anchor.Value);
                }
            }
            else
            {
                var replacement = new List<DimensionAnchor>(command.Anchors.Count);
                var seenValues = new HashSet<int>();
                foreach (var input in command.Anchors)
                {
                    var anchor = DimensionAnchor.Create(input.Value, input.Label);
                    if (!seenValues.Add(anchor.Value))
                    {
                        throw new DomainException(
                            $"No se permiten anclajes duplicados para el valor {anchor.Value} en la misma versión.");
                    }

                    finalScale.EnsureWithinScale(anchor.Value);
                    replacement.Add(anchor);
                }

                finalAnchors = replacement;
            }

            var candidate = DimensionVersion.Rehydrate(
                version.DimensionVersionId,
                version.DimensionId,
                version.Version,
                finalScale,
                command.Definition ?? version.Definition,
                version.Status,
                finalAnchors);

            var definitionChanged = candidate.Definition != version.Definition;
            var minChanged = candidate.Scale.MinValue != version.Scale.MinValue;
            var maxChanged = candidate.Scale.MaxValue != version.Scale.MaxValue;
            var stepChanged = candidate.Scale.Step != version.Scale.Step;
            var scaleChanged = minChanged || maxChanged || stepChanged;

            var previousAnchors = version.Anchors.ToDictionary(anchor => anchor.Value);
            var finalAnchorMap = candidate.Anchors.ToDictionary(anchor => anchor.Value);
            var removedValues = previousAnchors.Keys
                .Where(value => !finalAnchorMap.ContainsKey(value))
                .OrderBy(value => value)
                .ToArray();
            var addedAnchors = candidate.Anchors
                .Where(anchor => !previousAnchors.ContainsKey(anchor.Value))
                .ToArray();
            var relabeledAnchors = candidate.Anchors
                .Where(anchor => previousAnchors.TryGetValue(anchor.Value, out var previous) &&
                    previous.Label != anchor.Label)
                .ToArray();

            if (!definitionChanged && !scaleChanged &&
                removedValues.Length == 0 && addedAnchors.Length == 0 &&
                relabeledAnchors.Length == 0)
            {
                throw new ValidationException(
                    "La solicitud no modifica ningún campo de la versión: no hay cambios efectivos que aplicar.");
            }

            var changedFields = new List<string>(4);
            if (definitionChanged)
            {
                changedFields.Add("definition");
            }

            if (minChanged)
            {
                changedFields.Add("min_value");
            }

            if (maxChanged)
            {
                changedFields.Add("max_value");
            }

            if (stepChanged)
            {
                changedFields.Add("step");
            }

            var audit = AuditEvent.CreateCatalogVersionUpdated(
                version.DimensionVersionId, userId.Value, changedFields);

            if (removedValues.Length > 0)
            {
                await _dimensionAdministrationRepository.DeleteAnchorsAsync(
                    version.DimensionVersionId, removedValues, tx, cancellationToken);
            }

            if (definitionChanged || scaleChanged)
            {
                await _dimensionAdministrationRepository.UpdateVersionDetailsAsync(
                    version.DimensionVersionId,
                    candidate.Definition,
                    candidate.Scale.MinValue,
                    candidate.Scale.MaxValue,
                    candidate.Scale.Step,
                    tx,
                    cancellationToken);
            }

            foreach (var anchor in relabeledAnchors)
            {
                await _dimensionAdministrationRepository.UpdateAnchorLabelAsync(
                    version.DimensionVersionId, anchor.Value, anchor.Label, tx, cancellationToken);
            }

            if (addedAnchors.Length > 0)
            {
                await _dimensionAdministrationRepository.InsertAnchorsAsync(
                    version.DimensionVersionId, addedAnchors, tx, cancellationToken);
            }

            await _auditEventRepository.AddAsync(audit, tx, cancellationToken);

            return DimensionCatalogResultMapper.ToResult(candidate);
        }, cancellationToken);
    }
}
