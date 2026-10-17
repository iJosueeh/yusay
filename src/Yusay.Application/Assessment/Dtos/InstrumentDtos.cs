namespace Yusay.Application.Assessment.Dtos;

public sealed record InstrumentResult(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose);

public sealed record InstrumentVersionResult(
    Guid InstrumentVersionId,
    Guid InstrumentId,
    int Version,
    string Status,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public sealed record InstrumentSummaryDto(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    int TotalVersions,
    int? PublishedVersion);

public sealed record InstrumentVersionSummaryDto(
    Guid InstrumentVersionId,
    int Version,
    string Status,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public sealed record InstrumentDetailsDto(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    IReadOnlyList<InstrumentVersionSummaryDto> Versions);
