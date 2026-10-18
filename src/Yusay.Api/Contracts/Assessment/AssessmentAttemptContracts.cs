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
