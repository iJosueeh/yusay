using Yusay.Domain.Common;
using Yusay.Domain.Tracking.Enums;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.Tracking.Entities;

public sealed class DimensionVersion
{
    private readonly Dictionary<int, DimensionAnchor> _anchors = new();

    public Guid DimensionVersionId { get; }
    public Guid DimensionId { get; }
    public int Version { get; }
    public DimensionScale Scale { get; private set; }
    public string Definition { get; private set; }
    public DimensionVersionStatus Status { get; private set; }

    public IReadOnlyList<DimensionAnchor> Anchors =>
        _anchors.Values.OrderBy(a => a.Value).ToArray();

    private DimensionVersion(
        Guid dimensionVersionId,
        Guid dimensionId,
        int version,
        DimensionScale scale,
        string definition,
        DimensionVersionStatus status,
        IEnumerable<DimensionAnchor>? anchors)
    {
        DimensionVersionId = dimensionVersionId;
        DimensionId = dimensionId;
        Version = version;
        Scale = scale;
        Definition = definition;
        Status = status;

        if (anchors is not null)
        {
            foreach (var anchor in anchors)
            {
                _anchors[anchor.Value] = anchor;
            }
        }
    }

    public static DimensionVersion CreateDraft(
        Guid dimensionId,
        int version,
        DimensionScale scale,
        string definition,
        IEnumerable<DimensionAnchor>? initialAnchors = null,
        Guid? dimensionVersionId = null)
    {
        var id = dimensionVersionId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión no puede ser un UUID vacío.");
        }

        if (dimensionId == Guid.Empty)
        {
            throw new DomainException("La versión debe pertenecer a una Dimensión válida (UUID no vacío).");
        }

        if (version <= 0)
        {
            throw new DomainException($"El número de versión debe ser un entero positivo mayor a cero (recibido: {version}).");
        }

        ValidateDefinition(definition);

        var anchorMap = new Dictionary<int, DimensionAnchor>();
        if (initialAnchors is not null)
        {
            foreach (var anchor in initialAnchors)
            {
                scale.EnsureWithinScale(anchor.Value);
                if (anchorMap.ContainsKey(anchor.Value))
                {
                    throw new DomainException(
                        $"No se permiten anclajes duplicados para el valor {anchor.Value} en la misma versión.");
                }
                anchorMap[anchor.Value] = anchor;
            }
        }

        return new DimensionVersion(
            id,
            dimensionId,
            version,
            scale,
            definition.Trim(),
            DimensionVersionStatus.Draft,
            anchorMap.Values);
    }

    public static DimensionVersion Rehydrate(
        Guid dimensionVersionId,
        Guid dimensionId,
        int version,
        DimensionScale scale,
        string definition,
        DimensionVersionStatus status,
        IEnumerable<DimensionAnchor>? anchors)
    {
        if (dimensionVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión no puede ser un UUID vacío.");
        }

        if (dimensionId == Guid.Empty)
        {
            throw new DomainException("La versión debe pertenecer a una Dimensión válida (UUID no vacío).");
        }

        if (version <= 0)
        {
            throw new DomainException($"El número de versión debe ser un entero positivo mayor a cero (recibido: {version}).");
        }

        ValidateDefinition(definition);

        if (!Enum.IsDefined(typeof(DimensionVersionStatus), status))
        {
            throw new DomainException($"El estado '{status}' no es un estado válido para DimensionVersion.");
        }

        var anchorMap = new Dictionary<int, DimensionAnchor>();
        if (anchors is not null)
        {
            foreach (var anchor in anchors)
            {
                if (anchorMap.ContainsKey(anchor.Value))
                {
                    throw new DomainException(
                        $"Estructura de anclajes corrupta: se detectó un anclaje duplicado para el valor {anchor.Value}.");
                }
                anchorMap[anchor.Value] = anchor;
            }
        }

        return new DimensionVersion(
            dimensionVersionId,
            dimensionId,
            version,
            scale,
            definition.Trim(),
            status,
            anchorMap.Values);
    }

    public void UpdateScale(DimensionScale newScale)
    {
        EnsureDraft("modificar la escala");

        foreach (var anchor in _anchors.Values)
        {
            if (!newScale.IsWithinScale(anchor.Value))
            {
                throw new DomainException(
                    $"El cambio de escala dejaría el anclaje existente con valor {anchor.Value} fuera de la nueva escala {newScale}.");
            }
        }

        Scale = newScale;
    }

    public void UpdateDefinition(string newDefinition)
    {
        EnsureDraft("modificar la definición");
        ValidateDefinition(newDefinition);
        Definition = newDefinition.Trim();
    }

    public void AddOrUpdateAnchor(int value, string label)
    {
        EnsureDraft("agregar o actualizar anclajes");
        var anchor = DimensionAnchor.Create(value, label);
        Scale.EnsureWithinScale(value);
        _anchors[value] = anchor;
    }

    public void RemoveAnchor(int value)
    {
        EnsureDraft("eliminar anclajes");
        _anchors.Remove(value);
    }

    public void Activate()
    {
        if (Status != DimensionVersionStatus.Draft)
        {
            throw new DomainException(
                $"Transición de estado inválida: no se puede activar una versión en estado {Status}. Solo DRAFT puede pasar a ACTIVE.");
        }

        foreach (var anchor in _anchors.Values)
        {
            if (!Scale.IsWithinScale(anchor.Value))
            {
                throw new DomainException(
                    $"No se puede activar la versión: el anclaje con valor {anchor.Value} no pertenece a la escala {Scale}.");
            }
        }

        Status = DimensionVersionStatus.Active;
    }

    public void Retire()
    {
        if (Status != DimensionVersionStatus.Active)
        {
            throw new DomainException(
                $"Transición de estado inválida: no se puede retirar una versión en estado {Status}. Solo ACTIVE puede pasar a RETIRED.");
        }

        Status = DimensionVersionStatus.Retired;
    }

    private void EnsureDraft(string operation)
    {
        if (Status != DimensionVersionStatus.Draft)
        {
            throw new DomainException(
                $"Operación rechazada: no se permite {operation} en una versión en estado {Status}. Solo el estado DRAFT es editable.");
        }
    }

    private static void ValidateDefinition(string definition)
    {
        if (string.IsNullOrWhiteSpace(definition))
        {
            throw new DomainException("La definición de la versión no puede estar vacía ni contener solo espacios.");
        }
    }
}
