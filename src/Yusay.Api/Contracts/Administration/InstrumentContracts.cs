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

public sealed record DraftReferenceContract(
    int ReferenceOrder,
    string Citation,
    string? Url);

public sealed record DraftOptionContract(
    int Position,
    string Label,
    int? Contribution);

public sealed record DraftQuestionContract(
    int Position,
    string Prompt,
    bool Required,
    IReadOnlyList<DraftOptionContract> Options);

public sealed record DraftInterpretationContract(
    string Label,
    string Description,
    string? Limitations,
    int LowerBound,
    int UpperBound);

public sealed record ReplaceDraftDefinitionRequest(
    IReadOnlyList<DraftReferenceContract>? References,
    IReadOnlyList<DraftQuestionContract>? Questions,
    IReadOnlyList<DraftInterpretationContract>? Interpretations);

public sealed record DraftReferenceResponse(
    int ReferenceOrder,
    string Citation,
    string? Url);

public sealed record DraftOptionResponse(
    Guid OptionId,
    int Position,
    string Label,
    int? Contribution);

public sealed record DraftQuestionResponse(
    Guid QuestionId,
    int Position,
    string Prompt,
    bool Required,
    IReadOnlyList<DraftOptionResponse> Options);

public sealed record DraftInterpretationResponse(
    Guid InterpretationId,
    string Label,
    string Description,
    string? Limitations,
    int LowerBound,
    int UpperBound);

public sealed record DraftDefinitionResponse(
    Guid InstrumentVersionId,
    Guid InstrumentId,
    int Version,
    string Status,
    IReadOnlyList<DraftReferenceResponse> References,
    IReadOnlyList<DraftQuestionResponse> Questions,
    IReadOnlyList<DraftInterpretationResponse> Interpretations);
