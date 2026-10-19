using Yusay.Api.Contracts.Content;

namespace Yusay.Api.Contracts.Assessment;

public sealed record AssessmentGuidanceInstrumentResponse(
    Guid InstrumentId,
    string Code,
    string Name,
    Guid InstrumentVersionId,
    int Version);

public sealed record AssessmentGuidanceResultResponse(
    int Score,
    DateTimeOffset CalculatedAt);

public sealed record AssessmentGuidanceInterpretationResponse(
    Guid InterpretationId,
    string Label,
    string Description,
    string? Limitations);

public sealed record AssessmentGuidanceResponse(
    Guid AttemptId,
    AssessmentGuidanceInstrumentResponse Instrument,
    AssessmentGuidanceResultResponse Result,
    AssessmentGuidanceInterpretationResponse Interpretation,
    IReadOnlyList<ResourceTopicResponse> Topics,
    IReadOnlyList<ResourceSummaryResponse> Resources,
    string Disclaimer);

