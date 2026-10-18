using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class Interpretation
{
    public Guid InterpretationId { get; }
    public Guid InstrumentVersionId { get; }
    public string Label { get; }
    public string Description { get; }
    public string? Limitations { get; }
    public int LowerBound { get; }
    public int UpperBound { get; }

    public Interpretation(
        Guid interpretationId,
        Guid instrumentVersionId,
        string label,
        string description,
        string? limitations,
        int lowerBound,
        int upperBound)
    {
        if (interpretationId == Guid.Empty)
        {
            throw new DomainException("El identificador de la interpretación no puede ser un UUID vacío.");
        }

        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("La interpretación debe pertenecer a una versión válida (UUID no vacío).");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("La etiqueta de la interpretación no puede estar vacía.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("La descripción de la interpretación no puede estar vacía.");
        }

        if (lowerBound > upperBound)
        {
            throw new DomainException($"El límite inferior ({lowerBound}) no puede ser mayor que el límite superior ({upperBound}).");
        }

        InterpretationId = interpretationId;
        InstrumentVersionId = instrumentVersionId;
        Label = label.Trim();
        Description = description.Trim();
        Limitations = string.IsNullOrWhiteSpace(limitations) ? null : limitations.Trim();
        LowerBound = lowerBound;
        UpperBound = upperBound;
    }

    public static Interpretation Create(
        Guid instrumentVersionId,
        string label,
        string description,
        string? limitations,
        int lowerBound,
        int upperBound,
        Guid? interpretationId = null) =>
        new(
            interpretationId ?? Guid.NewGuid(),
            instrumentVersionId,
            label,
            description,
            limitations,
            lowerBound,
            upperBound);
}
