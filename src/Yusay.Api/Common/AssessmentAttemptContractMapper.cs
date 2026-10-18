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
}
