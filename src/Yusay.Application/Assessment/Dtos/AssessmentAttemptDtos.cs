namespace Yusay.Application.Assessment.Dtos;

public sealed record AttemptOptionDto(
    Guid OptionId,
    int Position,
    string Label);

public sealed record AttemptQuestionDto(
    Guid QuestionId,
    int Position,
    string Prompt,
    bool Required,
    IReadOnlyList<AttemptOptionDto> Options);

public sealed record StartAssessmentAttemptResult(
    Guid AttemptId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<AttemptQuestionDto> Questions);

public sealed record PublishedVersionHeaderDto(
    Guid InstrumentVersionId,
    int Version);

public sealed record ExistingAttemptHeaderDto(
    Guid AttemptId,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt);

public sealed record AssessmentAnswerItemDto(
    Guid QuestionId,
    Guid OptionId);

public sealed record SaveAssessmentAnswersResult(
    Guid AttemptId,
    int SavedCount);

public sealed record AttemptQuestionProgressDto(
    Guid QuestionId,
    int Position,
    string Prompt,
    bool Required,
    Guid? SelectedOptionId,
    IReadOnlyList<AttemptOptionDto> Options);

public sealed record AssessmentResultDetailDto(
    int Score,
    AssessmentInterpretationResultDto Interpretation,
    DateTimeOffset CalculatedAt);

public sealed record AssessmentAttemptProgressResult(
    Guid AttemptId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<AttemptQuestionProgressDto> Questions,
    AssessmentResultDetailDto? Result = null);

public sealed record AssessmentResultSummaryDto(
    int Score,
    string InterpretationLabel);

public sealed record AssessmentListItemDto(
    Guid AttemptId,
    Guid InstrumentId,
    string InstrumentCode,
    string InstrumentName,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset ExpiresAt,
    AssessmentResultSummaryDto? Result);

public sealed record ListAssessmentsResult(
    IReadOnlyList<AssessmentListItemDto> Items,
    string? NextCursor);

public sealed record AssessmentAttemptListItemRowDto(
    Guid AttemptId,
    Guid InstrumentId,
    string InstrumentCode,
    string InstrumentName,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    DateTimeOffset ExpiresAt,
    int? Score,
    string? InterpretationLabel);

public sealed record AttemptDetailsHeaderDto(
    Guid AttemptId,
    Guid UserId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? EndedAt);

public sealed record AssessmentInterpretationResultDto(
    Guid InterpretationId,
    string Label,
    string Description,
    string? Limitations);

public sealed record SubmitAssessmentAttemptResult(
    Guid AttemptId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int Score,
    AssessmentInterpretationResultDto Interpretation);

public sealed record InterpretationDataDto(
    Guid InterpretationId,
    string Label,
    string Description,
    string? Limitations,
    int LowerBound,
    int UpperBound);

public sealed record AssessmentSubmissionDataDto(
    IReadOnlyList<Guid> RequiredQuestionIds,
    IReadOnlyList<(Guid QuestionId, Guid OptionId)> UserAnswers,
    IReadOnlyDictionary<(Guid QuestionId, Guid OptionId), int> Contributions,
    IReadOnlyList<InterpretationDataDto> Interpretations);
