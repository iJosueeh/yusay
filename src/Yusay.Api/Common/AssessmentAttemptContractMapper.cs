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
                    o.Label)).ToList())).ToList(),
            result.Result is null
                ? null
                : new AssessmentResultDetailsResponse(
                    result.Result.Score,
                    new AssessmentInterpretationResponse(
                        result.Result.Interpretation.InterpretationId,
                        result.Result.Interpretation.Label,
                        result.Result.Interpretation.Description,
                        result.Result.Interpretation.Limitations),
                    result.Result.CalculatedAt));

    public static ListAssessmentsResponse ToResponse(ListAssessmentsResult result) =>
        new(
            result.Items.Select(item => new AssessmentListItemResponse(
                item.AttemptId,
                item.InstrumentId,
                item.InstrumentCode,
                item.InstrumentName,
                item.InstrumentVersionId,
                item.Version,
                item.Status,
                item.StartedAt,
                item.EndedAt,
                item.ExpiresAt,
                item.Result is null
                    ? null
                    : new AssessmentResultSummaryResponse(
                        item.Result.Score,
                        item.Result.InterpretationLabel))).ToList(),
            result.NextCursor);

    public static SaveAssessmentAnswersResponse ToResponse(SaveAssessmentAnswersResult result) =>
        new(result.AttemptId, result.SavedCount);

    public static IReadOnlyList<AssessmentAnswerItemDto> ToDtos(IReadOnlyList<AssessmentAnswerItemRequest>? items) =>
        items?.Select(i => new AssessmentAnswerItemDto(i.QuestionId, i.OptionId)).ToList()
        ?? (IReadOnlyList<AssessmentAnswerItemDto>)Array.Empty<AssessmentAnswerItemDto>();

    public static SubmitAssessmentAttemptResponse ToResponse(SubmitAssessmentAttemptResult result) =>
        new(
            result.AttemptId,
            result.InstrumentId,
            result.InstrumentVersionId,
            result.Version,
            result.Status,
            result.StartedAt,
            result.EndedAt,
            result.Score,
            new AssessmentInterpretationResponse(
                result.Interpretation.InterpretationId,
                result.Interpretation.Label,
                result.Interpretation.Description,
                result.Interpretation.Limitations));

    public static AssessmentGuidanceResponse ToGuidanceResponse(AssessmentGuidanceDto dto) =>
        new(
            dto.AttemptId,
            new AssessmentGuidanceInstrumentResponse(
                dto.Instrument.InstrumentId,
                dto.Instrument.Code,
                dto.Instrument.Name,
                dto.Instrument.InstrumentVersionId,
                dto.Instrument.Version),
            new AssessmentGuidanceResultResponse(
                dto.Result.Score,
                dto.Result.CalculatedAt),
            new AssessmentGuidanceInterpretationResponse(
                dto.Interpretation.InterpretationId,
                dto.Interpretation.Label,
                dto.Interpretation.Description,
                dto.Interpretation.Limitations),
            dto.Topics.Select(t => new Yusay.Api.Contracts.Content.ResourceTopicResponse(t.TopicId, t.Code, t.Name)).ToList(),
            dto.Resources.Select(PublishedContentContractMapper.ToResponse).ToList(),
            dto.Disclaimer);

}
