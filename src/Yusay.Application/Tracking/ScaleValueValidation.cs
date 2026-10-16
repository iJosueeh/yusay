using Yusay.Application.Common.Exceptions;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Application.Tracking;

public static class ScaleValueValidation
{
    public static void EnsureReachable(Guid dimensionId, int value, DimensionScale scale)
    {
        if (value < scale.MinValue || value > scale.MaxValue)
        {
            throw new ValidationException(
                $"El valor {value} de la Dimensión {dimensionId} está fuera de la escala [{scale.MinValue}, {scale.MaxValue}].");
        }

        var offsetFromMin = (long)value - scale.MinValue;
        if (offsetFromMin % scale.Step != 0)
        {
            throw new ValidationException(
                $"El valor {value} de la Dimensión {dimensionId} no es alcanzable con paso {scale.Step} desde el mínimo {scale.MinValue}.");
        }
    }
}
