using Yusay.Api.Contracts.Administration;
using Yusay.Application.Content.Dtos;

namespace Yusay.Api.Common;

public static class ContentContractMapper
{
    public static TopicResponse ToResponse(TopicDto dto) =>
        new(dto.TopicId, dto.Code, dto.Name, dto.Description);

    public static ListTopicsResponse ToListResponse(IEnumerable<TopicDto> dtos) =>
        new(dtos.Select(ToResponse).ToList());

    public static ResourceDetailsResponse ToResponse(ResourceDetailsDto dto) =>
        new(
            dto.ResourceId,
            dto.Type,
            dto.Status,
            dto.Title,
            dto.Summary,
            dto.Body,
            dto.ExternalUrl,
            dto.Topics.Select(t => new ResourceTopicResponse(t.TopicId, t.Code, t.Name)).ToList());

    public static ResourceSummaryResponse ToResponse(ResourceSummaryDto dto) =>
        new(
            dto.ResourceId,
            dto.Type,
            dto.Status,
            dto.Title,
            dto.Summary,
            dto.TopicCount,
            dto.TopicCodes);

    public static ListResourcesResponse ToListResponse(IEnumerable<ResourceSummaryDto> dtos) =>
        new(dtos.Select(ToResponse).ToList());
}
