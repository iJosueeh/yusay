using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;

namespace Yusay.Infrastructure.Content.Repositories;

public sealed class ResourceCatalogRepository(IDbConnectionFactory connectionFactory)
    : IResourceCatalogRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<ResourceDetailsDto?> GetPublishedByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        const string sqlResource = """
            SELECT resource_id, type, status, title, summary, body, external_url
            FROM yusay.resource
            WHERE resource_id = @ResourceId AND status = 'PUBLISHED';
            """;

        const string sqlTopics = """
            SELECT t.topic_id, t.code, t.name
            FROM yusay.resource_topic rt
            INNER JOIN yusay.topic t ON rt.topic_id = t.topic_id
            WHERE rt.resource_id = @ResourceId
            ORDER BY t.code ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ResourceRow>(
            new CommandDefinition(sqlResource, new { ResourceId = resourceId }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var topicRows = await connection.QueryAsync<ResourceTopicRow>(
            new CommandDefinition(sqlTopics, new { ResourceId = resourceId }, cancellationToken: cancellationToken));

        var topics = topicRows
            .Select(t => new ResourceTopicDto(t.topic_id, t.code, t.name))
            .ToList();

        return new ResourceDetailsDto(
            row.Resource_id,
            row.Type,
            row.Status,
            row.Title,
            row.Summary,
            row.Body,
            row.External_url,
            topics);
    }

    public async Task<IReadOnlyList<ResourceSummaryDto>> ListPublishedAsync(
        Guid? topicId,
        Guid? dimensionId,
        Guid? interpretationId,
        string? type,
        int limit,
        string? cursorTitle,
        Guid? cursorResourceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT 
                r.resource_id, 
                r.type, 
                r.status, 
                r.title, 
                r.summary,
                COUNT(rt.topic_id)::int AS topic_count,
                COALESCE(array_agg(t.code ORDER BY t.code) FILTER (WHERE t.code IS NOT NULL), '{}') AS topic_codes
            FROM yusay.resource r
            LEFT JOIN yusay.resource_topic rt ON r.resource_id = rt.resource_id
            LEFT JOIN yusay.topic t ON rt.topic_id = t.topic_id
            WHERE r.status = 'PUBLISHED'
              AND (@Type IS NULL OR r.type = @Type)
              AND (
                    @TopicId IS NULL OR EXISTS (
                        SELECT 1 FROM yusay.resource_topic sub_rt 
                        WHERE sub_rt.resource_id = r.resource_id AND sub_rt.topic_id = @TopicId
                    )
              )
              AND (
                    @DimensionId IS NULL OR EXISTS (
                        SELECT 1 FROM yusay.resource_topic sub_rt
                        INNER JOIN yusay.dimension_topic dt ON sub_rt.topic_id = dt.topic_id
                        WHERE sub_rt.resource_id = r.resource_id AND dt.dimension_id = @DimensionId
                    )
              )
              AND (
                    @InterpretationId IS NULL OR EXISTS (
                        SELECT 1 FROM yusay.resource_topic sub_rt
                        INNER JOIN yusay.interpretation_topic it ON sub_rt.topic_id = it.topic_id
                        WHERE sub_rt.resource_id = r.resource_id AND it.interpretation_id = @InterpretationId
                    )
              )
              AND (
                    @CursorTitle IS NULL OR (r.title > @CursorTitle) 
                    OR (r.title = @CursorTitle AND r.resource_id > @CursorResourceId)
              )
            GROUP BY r.resource_id, r.type, r.status, r.title, r.summary
            ORDER BY r.title ASC, r.resource_id ASC
            LIMIT @Limit;
            """;

        var parameters = new
        {
            Type = string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToUpperInvariant(),
            TopicId = topicId,
            DimensionId = dimensionId,
            InterpretationId = interpretationId,
            CursorTitle = cursorTitle,
            CursorResourceId = cursorResourceId,
            Limit = limit
        };

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ResourceSummaryRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return rows.Select(r => new ResourceSummaryDto(
            r.Resource_id,
            r.Type,
            r.Status,
            r.Title,
            r.Summary,
            r.Topic_count,
            r.Topic_codes ?? Array.Empty<string>())).ToList();
    }

    public async Task<Guid?> GetInterpretationIdForSubmittedAttemptAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ar.interpretation_id
            FROM yusay.assessment_attempt aa
            INNER JOIN yusay.assessment_result ar ON aa.attempt_id = ar.attempt_id
            WHERE aa.attempt_id = @AttemptId
              AND aa.user_id = @UserId
              AND aa.status = 'SUBMITTED';
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<Guid?>(
            new CommandDefinition(sql, new { AttemptId = attemptId, UserId = userId }, cancellationToken: cancellationToken));
    }

    private sealed record ResourceRow(
        Guid Resource_id,
        string Type,
        string Status,
        string Title,
        string? Summary,
        string? Body,
        string? External_url);

    private sealed record ResourceTopicRow(Guid topic_id, string code, string name);

    private sealed class ResourceSummaryRow
    {
        public Guid Resource_id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public int Topic_count { get; set; }
        public string[]? Topic_codes { get; set; }
    }
}
