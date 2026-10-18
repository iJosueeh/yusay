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

    public void MarkReady()
    {
        if (Status == InstrumentVersionStatus.Ready)
        {
            return;
        }

        if (Status != InstrumentVersionStatus.Draft)
        {
            throw new DomainException(
                $"Transición de estado inválida: no se puede marcar como READY una versión en estado {Status}. Solo DRAFT puede pasar a READY.");
        }

        Status = InstrumentVersionStatus.Ready;
    }

    public void ReturnToDraft()
    {
        if (Status == InstrumentVersionStatus.Draft)
        {
            return;
        }

        if (Status != InstrumentVersionStatus.Ready)
        {
            throw new DomainException(
                $"Transición de estado inválida: no se puede devolver a DRAFT una versión en estado {Status}. Solo READY puede volver a DRAFT.");
        }

        Status = InstrumentVersionStatus.Draft;
    }

    public void Publish()
    {
        if (Status == InstrumentVersionStatus.Published)
        {
            return;
        }

        if (Status != InstrumentVersionStatus.Ready)
        {
            throw new DomainException(
                $"Transición de estado inválida: no se puede publicar una versión en estado {Status}. Solo READY puede pasar a PUBLISHED.");
        }

        Status = InstrumentVersionStatus.Published;
    }

    public void Retire()
    {
        if (Status == InstrumentVersionStatus.Retired)
        {
            return;
        }

        if (Status != InstrumentVersionStatus.Published)
        {
            throw new DomainException(
                $"Transición de estado inválida: no se puede retirar una versión en estado {Status}. Solo PUBLISHED puede pasar a RETIRED.");
        }

        Status = InstrumentVersionStatus.Retired;
    }
}
