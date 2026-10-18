using Yusay.Application.Assessment.Dtos;

namespace Yusay.Application.Assessment.Commands.SaveAssessmentAnswers;

public sealed record SaveAssessmentAnswersCommand(
    Guid AttemptId,
    IReadOnlyList<AssessmentAnswerItemDto> Answers);
