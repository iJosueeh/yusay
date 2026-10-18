using Yusay.Api.Contracts.Assessment;
using Yusay.Application.Assessment.Dtos;

namespace Yusay.Api.Common;

public static class PublishedInstrumentContractMapper
{
    public static ListPublishedInstrumentsResponse ToListResponse(IReadOnlyList<PublishedInstrumentSummaryDto> items) =>
        new(items.Select(i => new PublishedInstrumentSummaryResponse(
            i.InstrumentId,
            i.Code,
            i.Name,
            i.Description,
            i.Purpose,
            new PublishedVersionSummaryResponse(
                i.PublishedVersion.InstrumentVersionId,
                i.PublishedVersion.Version,
                i.PublishedVersion.SourceDescription,
                i.PublishedVersion.Population,
                i.PublishedVersion.AdministrationConditions,
                i.PublishedVersion.LicenseInformation,
                i.PublishedVersion.Limitations))).ToList());

    public static PublishedInstrumentDetailsResponse ToDetailsResponse(PublishedInstrumentDetailsDto dto) =>
        new(
            dto.InstrumentId,
            dto.Code,
            dto.Name,
            dto.Description,
            dto.Purpose,
            new PublishedVersionDetailsResponse(
                dto.Version.InstrumentVersionId,
                dto.Version.Version,
                dto.Version.SourceDescription,
                dto.Version.Population,
                dto.Version.AdministrationConditions,
                dto.Version.LicenseInformation,
                dto.Version.Limitations,
                dto.Version.References.Select(r => new PublishedInstrumentReferenceResponse(
                    r.ReferenceOrder,
                    r.Citation,
                    r.Url)).ToList()));
}
