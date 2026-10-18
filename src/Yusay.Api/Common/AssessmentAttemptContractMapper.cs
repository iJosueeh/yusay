using Yusay.Api.Contracts.Assessment;
using Yusay.Application.Assessment.Dtos;

namespace Yusay.Api.Common;

public static class AssessmentAttemptContractMapper
{
    public static StartAssessmentAttemptResponse ToResponse(StartAssessmentAttemptResult result) =>
        new(
            result.AttemptId,
            result.InstrumentId,
            result.InstrumentVersionId,
            result.Version,
            result.Status,
            result.StartedAt,
            result.ExpiresAt,
            result.Questions.Select(q => new AttemptQuestionResponse(
                q.QuestionId,
                q.Position,
                q.Prompt,
                q.Required,
                q.Options.Select(o => new AttemptOptionResponse(
                    o.OptionId,
                    o.Position,
                    o.Label)).ToList())).ToList());

    public static AssessmentAttemptProgressResponse ToResponse(AssessmentAttemptProgressResult result) =>
        new(
            result.AttemptId,
            result.InstrumentId,
            result.InstrumentVersionId,
            result.Version,
            result.Status,
            result.StartedAt,
            result.ExpiresAt,
            result.Questions.Select(q => new AttemptQuestionProgressResponse(
                q.QuestionId,
                q.Position,
                q.Prompt,
                q.Required,
                q.SelectedOptionId,
                q.Options.Select(o => new AttemptOptionResponse(
                    o.OptionId,
                    o.Position,
                    o.Label)).ToList())).ToList());

    public static SaveAssessmentAnswersResponse ToResponse(SaveAssessmentAnswersResult result) =>
        new(result.AttemptId, result.SavedCount);

    public static IReadOnlyList<AssessmentAnswerItemDto> ToDtos(IReadOnlyList<AssessmentAnswerItemRequest>? items) =>
        items?.Select(i => new AssessmentAnswerItemDto(i.QuestionId, i.OptionId)).ToList()
        ?? (IReadOnlyList<AssessmentAnswerItemDto>)Array.Empty<AssessmentAnswerItemDto>();
}
