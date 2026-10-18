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

public sealed record DraftReferenceDto(
    int ReferenceOrder,
    string Citation,
    string? Url);

public sealed record DraftOptionDto(
    Guid OptionId,
    int Position,
    string Label,
    int? Contribution);

public sealed record DraftQuestionDto(
    Guid QuestionId,
    int Position,
    string Prompt,
    bool Required,
    IReadOnlyList<DraftOptionDto> Options);

public sealed record DraftInterpretationDto(
    Guid InterpretationId,
    string Label,
    string Description,
    string? Limitations,
    int LowerBound,
    int UpperBound);

public sealed record DraftDefinitionDto(
    Guid InstrumentVersionId,
    Guid InstrumentId,
    int Version,
    string Status,
    IReadOnlyList<DraftReferenceDto> References,
    IReadOnlyList<DraftQuestionDto> Questions,
    IReadOnlyList<DraftInterpretationDto> Interpretations);
