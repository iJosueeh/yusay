using Yusay.Api.Contracts.Administration;
using Yusay.Application.Assessment.Dtos;

namespace Yusay.Api.Common;

public static class InstrumentContractMapper
{
    public static InstrumentResponse ToResponse(InstrumentResult result) =>
        new(
            result.InstrumentId,
            result.Code,
            result.Name,
            result.Description,
            result.Purpose);

    public static InstrumentVersionResponse ToResponse(InstrumentVersionResult result) =>
        new(
            result.InstrumentVersionId,
            result.InstrumentId,
            result.Version,
            result.Status,
            result.SourceDescription,
            result.Population,
            result.AdministrationConditions,
            result.LicenseInformation,
            result.Limitations);

    public static ListInstrumentsAdminResponse ToListResponse(IReadOnlyList<InstrumentSummaryDto> items) =>
        new(items.Select(i => new InstrumentSummaryResponse(
            i.InstrumentId,
            i.Code,
            i.Name,
            i.Description,
            i.Purpose,
            i.TotalVersions,
            i.PublishedVersion)).ToList());

    public static InstrumentDetailsAdminResponse ToDetailsResponse(InstrumentDetailsDto dto) =>
        new(
            dto.InstrumentId,
            dto.Code,
            dto.Name,
            dto.Description,
            dto.Purpose,
            dto.Versions.Select(v => new InstrumentVersionSummaryResponse(
                v.InstrumentVersionId,
                v.Version,
                v.Status,
                v.SourceDescription,
                v.Population,
                v.AdministrationConditions,
                v.LicenseInformation,
                v.Limitations)).ToList());

    public static DraftDefinitionResponse ToResponse(DraftDefinitionDto dto) =>
        new(
            dto.InstrumentVersionId,
            dto.InstrumentId,
            dto.Version,
            dto.Status,
            dto.References.Select(r => new DraftReferenceResponse(r.ReferenceOrder, r.Citation, r.Url)).ToList(),
            dto.Questions.Select(q => new DraftQuestionResponse(
                q.QuestionId,
                q.Position,
                q.Prompt,
                q.Required,
                q.Options.Select(o => new DraftOptionResponse(o.OptionId, o.Position, o.Label, o.Contribution)).ToList())).ToList(),
            dto.Interpretations.Select(i => new DraftInterpretationResponse(
                i.InterpretationId,
                i.Label,
                i.Description,
                i.Limitations,
                i.LowerBound,
                i.UpperBound)).ToList());
}
