using Yusay.Domain.Common;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.Tracking.Entities;

public sealed class Dimension
{
    public Guid DimensionId { get; }
    public DimensionCode Code { get; }
    public string Name { get; private set; }
    public string Description { get; private set; }

    private Dimension(
        Guid dimensionId,
        DimensionCode code,
        string name,
        string description)
    {
        DimensionId = dimensionId;
        Code = code;
        Name = name;
        Description = description;
    }

    public static Dimension Create(
        DimensionCode code,
        string name,
        string description,
        Guid? dimensionId = null)
    {
        var id = dimensionId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador de la dimensión no puede ser un UUID vacío.");
        }

        ValidateDetails(name, description);

        return new Dimension(
            id,
            code,
            name.Trim(),
            description.Trim());
    }

    public static Dimension Rehydrate(
        Guid dimensionId,
        DimensionCode code,
        string name,
        string description)
    {
        if (dimensionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la dimensión no puede ser un UUID vacío.");
        }

        ValidateDetails(name, description);

        return new Dimension(
            dimensionId,
            code,
            name.Trim(),
            description.Trim());
    }

    public void UpdateDetails(string name, string description)
    {
        ValidateDetails(name, description);
        Name = name.Trim();
        Description = description.Trim();
    }

    private static void ValidateDetails(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre de la dimensión no puede estar vacío ni contener solo espacios.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("La descripción de la dimensión no puede estar vacía ni contener solo espacios.");
        }
    }
}
