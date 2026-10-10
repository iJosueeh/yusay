using Yusay.Application.Common.Exceptions;

namespace Yusay.Application.Tracking;

public static class ScaleValueValidation
{
    public static void EnsureReachable(Guid dimensionId, int value, int minValue, int maxValue, int step)
    {
        if (value < minValue || value > maxValue)
        {
            throw new ValidationException(
                $"El valor {value} de la Dimensión {dimensionId} está fuera de la escala [{minValue}, {maxValue}].");
        }

        var offsetFromMin = (long)value - minValue;
        if (offsetFromMin % step != 0)
        {
            throw new ValidationException(
                $"El valor {value} de la Dimensión {dimensionId} no es alcanzable con paso {step} desde el mínimo {minValue}.");
        }
    }
}
