using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class InstrumentVersion
{
    public Guid InstrumentVersionId { get; }
    public Guid InstrumentId { get; }
    public int Version { get; }
    public InstrumentVersionStatus Status { get; private set; }
    public string SourceDescription { get; private set; }
    public string Population { get; private set; }
    public string? AdministrationConditions { get; private set; }
    public string? LicenseInformation { get; private set; }
    public string Limitations { get; private set; }

    private InstrumentVersion(
        Guid instrumentVersionId,
        Guid instrumentId,
        int version,
        InstrumentVersionStatus status,
        string sourceDescription,
        string population,
        string? administrationConditions,
        string? licenseInformation,
        string limitations)
    {
        InstrumentVersionId = instrumentVersionId;
        InstrumentId = instrumentId;
        Version = version;
        Status = status;
        SourceDescription = sourceDescription;
        Population = population;
        AdministrationConditions = administrationConditions;
        LicenseInformation = licenseInformation;
        Limitations = limitations;
    }

    public static InstrumentVersion CreateDraft(
        Guid instrumentId,
        int version,
        string sourceDescription,
        string population,
        string? administrationConditions,
        string? licenseInformation,
        string limitations,
        Guid? instrumentVersionId = null)
    {
        var id = instrumentVersionId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión no puede ser un UUID vacío.");
        }

        if (instrumentId == Guid.Empty)
        {
            throw new DomainException("La versión debe pertenecer a un Instrumento válido (UUID no vacío).");
        }

        if (version <= 0)
        {
            throw new DomainException($"El número de versión debe ser un entero positivo mayor a cero (recibido: {version}).");
        }

        ValidateMetadata(sourceDescription, population, limitations);

        return new InstrumentVersion(
            id,
            instrumentId,
            version,
            InstrumentVersionStatus.Draft,
            sourceDescription.Trim(),
            population.Trim(),
            string.IsNullOrWhiteSpace(administrationConditions) ? null : administrationConditions.Trim(),
            string.IsNullOrWhiteSpace(licenseInformation) ? null : licenseInformation.Trim(),
            limitations.Trim());
    }

    public static InstrumentVersion Rehydrate(
        Guid instrumentVersionId,
        Guid instrumentId,
        int version,
        InstrumentVersionStatus status,
        string sourceDescription,
        string population,
        string? administrationConditions,
        string? licenseInformation,
        string limitations)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión no puede ser un UUID vacío.");
        }

        if (instrumentId == Guid.Empty)
        {
            throw new DomainException("La versión debe pertenecer a un Instrumento válido (UUID no vacío).");
        }

        if (version <= 0)
        {
            throw new DomainException($"El número de versión debe ser un entero positivo mayor a cero (recibido: {version}).");
        }

        ValidateMetadata(sourceDescription, population, limitations);

        return new InstrumentVersion(
            instrumentVersionId,
            instrumentId,
            version,
            status,
            sourceDescription.Trim(),
            population.Trim(),
            string.IsNullOrWhiteSpace(administrationConditions) ? null : administrationConditions.Trim(),
            string.IsNullOrWhiteSpace(licenseInformation) ? null : licenseInformation.Trim(),
            limitations.Trim());
    }

    private static void ValidateMetadata(string sourceDescription, string population, string limitations)
    {
        if (string.IsNullOrWhiteSpace(sourceDescription))
        {
            throw new DomainException("La descripción de la fuente bibliográfica no puede estar vacía.");
        }

        if (string.IsNullOrWhiteSpace(population))
        {
            throw new DomainException("La descripción de la población objetivo no puede estar vacía.");
        }

        if (string.IsNullOrWhiteSpace(limitations))
        {
            throw new DomainException("Las limitaciones psicométricas del instrumento no pueden estar vacías.");
        }
    }
}
