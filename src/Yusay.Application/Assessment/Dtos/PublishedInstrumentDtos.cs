namespace Yusay.Application.Assessment.Dtos;

public sealed record PublishedInstrumentReferenceDto(
    int ReferenceOrder,
    string Citation,
    string? Url);

public sealed record PublishedVersionSummaryDto(
    Guid InstrumentVersionId,
    int Version,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public sealed record PublishedInstrumentSummaryDto(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    PublishedVersionSummaryDto PublishedVersion);

public sealed record PublishedVersionDetailsDto(
    Guid InstrumentVersionId,
    int Version,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations,
    IReadOnlyList<PublishedInstrumentReferenceDto> References);

public sealed record PublishedInstrumentDetailsDto(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    PublishedVersionDetailsDto Version);
