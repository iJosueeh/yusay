namespace Yusay.Api.Contracts.Assessment;

public sealed record PublishedInstrumentReferenceResponse(
    int ReferenceOrder,
    string Citation,
    string? Url);

public sealed record PublishedVersionSummaryResponse(
    Guid InstrumentVersionId,
    int Version,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public sealed record PublishedInstrumentSummaryResponse(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    PublishedVersionSummaryResponse PublishedVersion);

public sealed record ListPublishedInstrumentsResponse(
    IReadOnlyList<PublishedInstrumentSummaryResponse> Items);

public sealed record PublishedVersionDetailsResponse(
    Guid InstrumentVersionId,
    int Version,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations,
    IReadOnlyList<PublishedInstrumentReferenceResponse> References);

public sealed record PublishedInstrumentDetailsResponse(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    PublishedVersionDetailsResponse Version);
