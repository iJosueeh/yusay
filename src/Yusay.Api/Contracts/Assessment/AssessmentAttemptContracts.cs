namespace Yusay.Api.Contracts.Assessment;

public sealed record AttemptOptionResponse(
    Guid OptionId,
    int Position,
    string Label);

public sealed record AttemptQuestionResponse(
    Guid QuestionId,
    int Position,
    string Prompt,
    bool Required,
    IReadOnlyList<AttemptOptionResponse> Options);

public sealed record StartAssessmentAttemptResponse(
    Guid AttemptId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<AttemptQuestionResponse> Questions);

public sealed record AssessmentAnswerItemRequest(
    Guid QuestionId,
    Guid OptionId);

public sealed record SaveAssessmentAnswersRequest(
    IReadOnlyList<AssessmentAnswerItemRequest> Answers);

public sealed record SaveAssessmentAnswersResponse(
    Guid AttemptId,
    int SavedCount);

public sealed record AttemptQuestionProgressResponse(
    Guid QuestionId,
    int Position,
    string Prompt,
    bool Required,
    Guid? SelectedOptionId,
    IReadOnlyList<AttemptOptionResponse> Options);

public sealed record AssessmentResultDetailsResponse(
    int Score,
    AssessmentInterpretationResponse Interpretation,
    DateTimeOffset CalculatedAt);

public sealed record AssessmentAttemptProgressResponse(
    Guid AttemptId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<AttemptQuestionProgressResponse> Questions,
    AssessmentResultDetailsResponse? Result = null);

public sealed record AssessmentResultSummaryResponse(
    int Score,
    string InterpretationLabel);

public sealed record AssessmentListItemResponse(
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
    AssessmentResultSummaryResponse? Result);

public sealed record ListAssessmentsResponse(
    IReadOnlyList<AssessmentListItemResponse> Items,
    string? NextCursor);

public sealed record AssessmentInterpretationResponse(
    Guid InterpretationId,
    string Label,
    string Description,
    string? Limitations);

public sealed record SubmitAssessmentAttemptResponse(
    Guid AttemptId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    int Score,
    AssessmentInterpretationResponse Interpretation);
