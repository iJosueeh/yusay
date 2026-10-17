namespace Yusay.Api.Contracts.Administration;

public sealed record CreateInstrumentRequest(
    string Code,
    string Name,
    string Description,
    string Purpose);

public sealed record UpdateInstrumentRequest(
    string? Name,
    string? Description,
    string? Purpose);

public sealed record CreateInstrumentVersionRequest(
    int? Version,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public sealed record InstrumentResponse(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose);

public sealed record InstrumentVersionResponse(
    Guid InstrumentVersionId,
    Guid InstrumentId,
    int Version,
    string Status,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public sealed record InstrumentSummaryResponse(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    int TotalVersions,
    int? PublishedVersion);

public sealed record ListInstrumentsAdminResponse(
    IReadOnlyList<InstrumentSummaryResponse> Items);

public sealed record InstrumentVersionSummaryResponse(
    Guid InstrumentVersionId,
    int Version,
    string Status,
    string SourceDescription,
    string Population,
    string? AdministrationConditions,
    string? LicenseInformation,
    string Limitations);

public sealed record InstrumentDetailsAdminResponse(
    Guid InstrumentId,
    string Code,
    string Name,
    string Description,
    string Purpose,
    IReadOnlyList<InstrumentVersionSummaryResponse> Versions);
