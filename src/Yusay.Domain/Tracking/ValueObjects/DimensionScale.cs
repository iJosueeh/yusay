using Yusay.Domain.Common;

namespace Yusay.Domain.Tracking.ValueObjects;

public readonly record struct DimensionScale
{
    public int MinValue { get; }
    public int MaxValue { get; }
    public int Step { get; }

    private DimensionScale(int minValue, int maxValue, int step)
    {
        MinValue = minValue;
        MaxValue = maxValue;
        Step = step;
    }

    public static DimensionScale Create(int minValue, int maxValue, int step)
    {
        if (minValue >= maxValue)
        {
            throw new DomainException(
                $"El extremo inferior ({minValue}) debe ser estrictamente menor al extremo superior ({maxValue}).");
        }

        if (step <= 0)
        {
            throw new DomainException(
                $"El incremento (step) debe ser un entero positivo mayor a cero (recibido: {step}).");
        }

        var range = (long)maxValue - minValue;
        if (range % step != 0)
        {
            throw new DomainException(
                $"El rango [{minValue}, {maxValue}] no es divisible exactamente entre el incremento {step}.");
        }

        return new DimensionScale(minValue, maxValue, step);
    }

    public bool IsWithinScale(int value)
    {
        if (Step <= 0 || MinValue >= MaxValue)
        {
            return false;
        }

        if (value < MinValue || value > MaxValue)
        {
            return false;
        }

        var offset = (long)value - MinValue;
        return offset % Step == 0;
    }

    public void EnsureWithinScale(int value)
    {
        if (!IsWithinScale(value))
        {
            throw new DomainException(
                $"El valor {value} no pertenece a la escala [{MinValue}, {MaxValue}] con incremento {Step}.");
        }
    }

    public override string ToString() => $"[{MinValue}..{MaxValue}, step: {Step}]";
}
