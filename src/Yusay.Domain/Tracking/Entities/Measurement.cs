using Yusay.Domain.Common;

namespace Yusay.Domain.Tracking.Entities;

public sealed class Measurement
{
    public Guid DimensionId { get; }
    public Guid DimensionVersionId { get; }
    public int Value { get; }

    private Measurement(Guid dimensionId, Guid dimensionVersionId, int value)
    {
        DimensionId = dimensionId;
        DimensionVersionId = dimensionVersionId;
        Value = value;
    }

    public static Measurement Create(Guid dimensionId, Guid dimensionVersionId, int value)
    {
        if (dimensionId == Guid.Empty)
        {
            throw new DomainException("La Measurement debe referenciar una Dimensión con identificador válido.");
        }

        if (dimensionVersionId == Guid.Empty)
        {
            throw new DomainException("La Measurement debe conservar la DimensionVersion exacta utilizada.");
        }

        return new Measurement(dimensionId, dimensionVersionId, value);
    }

    public static Measurement Rehydrate(Guid dimensionId, Guid dimensionVersionId, int value) =>
        new(dimensionId, dimensionVersionId, value);
}
