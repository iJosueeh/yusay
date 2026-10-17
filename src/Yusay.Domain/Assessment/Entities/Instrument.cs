using Yusay.Domain.Assessment.ValueObjects;
using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class Instrument
{
    public Guid InstrumentId { get; }
    public InstrumentCode Code { get; }
    public string Name { get; private set; }
    public string Description { get; private set; }
    public string Purpose { get; private set; }

    private Instrument(
        Guid instrumentId,
        InstrumentCode code,
        string name,
        string description,
        string purpose)
    {
        InstrumentId = instrumentId;
        Code = code;
        Name = name;
        Description = description;
        Purpose = purpose;
    }

    public static Instrument Create(
        InstrumentCode code,
        string name,
        string description,
        string purpose,
        Guid? instrumentId = null)
    {
        var id = instrumentId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador del instrumento no puede ser un UUID vacío.");
        }

        ValidateDetails(name, description, purpose);

        return new Instrument(
            id,
            code,
            name.Trim(),
            description.Trim(),
            purpose.Trim());
    }

    public static Instrument Rehydrate(
        Guid instrumentId,
        InstrumentCode code,
        string name,
        string description,
        string purpose)
    {
        if (instrumentId == Guid.Empty)
        {
            throw new DomainException("El identificador del instrumento no puede ser un UUID vacío.");
        }

        ValidateDetails(name, description, purpose);

        return new Instrument(
            instrumentId,
            code,
            name.Trim(),
            description.Trim(),
            purpose.Trim());
    }

    public void UpdateDetails(string name, string description, string purpose)
    {
        ValidateDetails(name, description, purpose);
        Name = name.Trim();
        Description = description.Trim();
        Purpose = purpose.Trim();
    }

    private static void ValidateDetails(string name, string description, string purpose)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del instrumento no puede estar vacío ni contener solo espacios.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainException("La descripción del instrumento no puede estar vacía ni contener solo espacios.");
        }

        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new DomainException("El propósito del instrumento no puede estar vacío ni contener solo espacios.");
        }
    }
}
