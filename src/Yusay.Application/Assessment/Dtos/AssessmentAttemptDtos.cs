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

public sealed record AssessmentAttemptProgressResult(
    Guid AttemptId,
    Guid InstrumentId,
    Guid InstrumentVersionId,
    int Version,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset ExpiresAt,
    IReadOnlyList<AttemptQuestionProgressDto> Questions);

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
