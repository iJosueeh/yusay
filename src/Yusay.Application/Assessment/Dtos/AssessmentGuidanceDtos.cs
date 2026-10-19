using Yusay.Application.Content.Dtos;

namespace Yusay.Application.Assessment.Dtos;

public sealed record AssessmentGuidanceAttemptDataDto(
    Guid AttemptId,
    Guid InstrumentId,
    string InstrumentCode,
    string InstrumentName,
    Guid InstrumentVersionId,
    int Version,
    int Score,
    DateTimeOffset CalculatedAt,
    Guid InterpretationId,
    string InterpretationLabel,
    string InterpretationDescription,
    string? InterpretationLimitations);

public sealed record AssessmentGuidanceInstrumentDto(
    Guid InstrumentId,
    string Code,
    string Name,
    Guid InstrumentVersionId,
    int Version);

public sealed record AssessmentGuidanceResultDto(
    int Score,
    DateTimeOffset CalculatedAt);

public sealed record AssessmentGuidanceInterpretationDto(
    Guid InterpretationId,
    string Label,
    string Description,
    string? Limitations);

public sealed record AssessmentGuidanceDto(
    Guid AttemptId,
    AssessmentGuidanceInstrumentDto Instrument,
    AssessmentGuidanceResultDto Result,
    AssessmentGuidanceInterpretationDto Interpretation,
    IReadOnlyList<ResourceTopicDto> Topics,
    IReadOnlyList<ResourceSummaryDto> Resources,
    string Disclaimer);

