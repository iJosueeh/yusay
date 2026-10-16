using Yusay.Domain.Common;

namespace Yusay.Domain.Tracking.ValueObjects;

public readonly record struct DimensionAnchor
{
    public int Value { get; }
    public string Label { get; }

    private DimensionAnchor(int value, string label)
    {
        Value = value;
        Label = label;
    }

    public static DimensionAnchor Create(int value, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("La etiqueta del anclaje no puede estar vacía ni contener solo espacios.");
        }

        return new DimensionAnchor(value, label.Trim());
    }

    public override string ToString() => $"{Value}: \"{Label}\"";
}
