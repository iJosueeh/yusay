using System.Data.Common;
using Dapper;
using Npgsql;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Dtos;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Content.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Infrastructure.Content.Repositories;

public sealed class ResourceAdministrationRepository(IDbConnectionFactory connectionFactory)
    : IResourceAdministrationRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<Resource?> GetByIdAsync(Guid resourceId, CancellationToken cancellationToken = default)
    {
        const string sqlResource = """
            SELECT resource_id, type, status, title, summary, body, external_url
            FROM yusay.resource
            WHERE resource_id = @ResourceId;
            """;

        const string sqlTopics = """
            SELECT topic_id
            FROM yusay.resource_topic
            WHERE resource_id = @ResourceId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var row = await connection.QuerySingleOrDefaultAsync<ResourceRow>(
            new CommandDefinition(sqlResource, new { ResourceId = resourceId }, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var topicIds = await connection.QueryAsync<Guid>(
            new CommandDefinition(sqlTopics, new { ResourceId = resourceId }, cancellationToken: cancellationToken));

        return MapResource(row, topicIds);
    }

    public async Task<Resource?> GetByIdForUpdateAsync(
        Guid resourceId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sqlResource = """
            SELECT resource_id, type, status, title, summary, body, external_url
            FROM yusay.resource
            WHERE resource_id = @ResourceId
            FOR UPDATE;
            """;

        const string sqlTopics = """
            SELECT topic_id
            FROM yusay.resource_topic
            WHERE resource_id = @ResourceId;
            """;

        var row = await transaction.Connection!.QuerySingleOrDefaultAsync<ResourceRow>(
            new CommandDefinition(sqlResource, new { ResourceId = resourceId }, transaction, cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var topicIds = await transaction.Connection!.QueryAsync<Guid>(
            new CommandDefinition(sqlTopics, new { ResourceId = resourceId }, transaction, cancellationToken: cancellationToken));

        return MapResource(row, topicIds);
    }

    public async Task<ResourceDetailsDto?> GetDetailsByIdAsync(
        Guid resourceId,
        CancellationToken cancellationToken = default)
    {
        const string sqlResource = """
            SELECT resource_id, type, status, title, summary, body, external_url
            FROM yusay.resource
            WHERE resource_id = @ResourceId;
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
            row.resource_id,
            row.type,
            row.status,
            row.title,
            row.summary,
            row.body,
            row.external_url,
            topics);
    }

    public async Task<IReadOnlyList<ResourceSummaryDto>> ListAsync(
        string? status = null,
        string? type = null,
        Guid? topicId = null,
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
            WHERE (@Status IS NULL OR r.status = @Status)
              AND (@Type IS NULL OR r.type = @Type)
              AND (@TopicId IS NULL OR EXISTS (
                    SELECT 1 FROM yusay.resource_topic sub_rt 
                    WHERE sub_rt.resource_id = r.resource_id AND sub_rt.topic_id = @TopicId
              ))
            GROUP BY r.resource_id, r.type, r.status, r.title, r.summary
            ORDER BY r.title ASC, r.resource_id ASC;
            """;

        var parameters = new
        {
            Status = string.IsNullOrWhiteSpace(status) ? null : status.Trim().ToUpperInvariant(),
            Type = string.IsNullOrWhiteSpace(type) ? null : type.Trim().ToUpperInvariant(),
            TopicId = topicId
        };

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

        var rows = await connection.QueryAsync<ResourceSummaryRow>(
            new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));

        return rows.Select(r => new ResourceSummaryDto(
            r.resource_id,
            r.type,
            r.status,
            r.title,
            r.summary,
            r.topic_count,
            r.topic_codes ?? Array.Empty<string>())).ToList();
    }

    public async Task AddAsync(
        Resource resource,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.resource (
                resource_id,
                type,
                status,
                title,
                summary,
                body,
                external_url
            ) VALUES (
                @ResourceId,
                @Type,
                @Status,
                @Title,
                @Summary,
                @Body,
                @ExternalUrl
            );
            """;

        var parameters = new
        {
            resource.ResourceId,
            Type = resource.Type.ToDbValue(),
            Status = resource.Status.ToDbValue(),
            resource.Title,
            resource.Summary,
            resource.Body,
            resource.ExternalUrl
        };

        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task UpdateDraftAsync(
        Resource resource,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.resource
            SET type = @Type,
                title = @Title,
                summary = @Summary,
                body = @Body,
                external_url = @ExternalUrl
            WHERE resource_id = @ResourceId;
            """;

        var parameters = new
        {
            resource.ResourceId,
            Type = resource.Type.ToDbValue(),
            resource.Title,
            resource.Summary,
            resource.Body,
            resource.ExternalUrl
        };

        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task UpdatePublishedCorrectionAsync(
        Resource resource,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.resource
            SET title = @Title,
                summary = @Summary,
                body = @Body,
                external_url = @ExternalUrl
            WHERE resource_id = @ResourceId;
            """;

        var parameters = new
        {
            resource.ResourceId,
            resource.Title,
            resource.Summary,
            resource.Body,
            resource.ExternalUrl
        };

        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task UpdateStatusAsync(
        Guid resourceId,
        ResourceStatus status,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE yusay.resource
            SET status = @Status
            WHERE resource_id = @ResourceId;
            """;

        var parameters = new
        {
            ResourceId = resourceId,
            Status = status.ToDbValue()
        };

        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task AddTopicAssociationAsync(
        Guid resourceId,
        Guid topicId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.resource_topic (resource_id, topic_id)
            VALUES (@ResourceId, @TopicId)
            ON CONFLICT DO NOTHING;
            """;

        var parameters = new { ResourceId = resourceId, TopicId = topicId };
        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task RemoveTopicAssociationAsync(
        Guid resourceId,
        Guid topicId,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            DELETE FROM yusay.resource_topic
            WHERE resource_id = @ResourceId AND topic_id = @TopicId;
            """;

        var parameters = new { ResourceId = resourceId, TopicId = topicId };
        var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
        await transaction.Connection!.ExecuteAsync(command);
    }

    public async Task<bool> HasTopicAssociationAsync(
        Guid resourceId,
        Guid topicId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT EXISTS (
                SELECT 1 FROM yusay.resource_topic
                WHERE resource_id = @ResourceId AND topic_id = @TopicId
            );
            """;

        var parameters = new { ResourceId = resourceId, TopicId = topicId };

        if (transaction?.Connection != null)
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
            return await transaction.Connection.ExecuteScalarAsync<bool>(command);
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var directCommand = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(directCommand);
    }

    public async Task<int> GetTopicCountAsync(
        Guid resourceId,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT COUNT(*)
            FROM yusay.resource_topic
            WHERE resource_id = @ResourceId;
            """;

        var parameters = new { ResourceId = resourceId };

        if (transaction?.Connection != null)
        {
            var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
            return await transaction.Connection.ExecuteScalarAsync<int>(command);
        }

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var directCommand = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
        return await connection.ExecuteScalarAsync<int>(directCommand);
    }

    private static Resource MapResource(ResourceRow row, IEnumerable<Guid> topicIds) =>
        Resource.Rehydrate(
            row.resource_id,
            ResourceTypeExtensions.FromDbValue(row.type),
            ResourceStatusExtensions.FromDbValue(row.status),
            row.title,
            row.summary,
            row.body,
            row.external_url,
            topicIds);

    private sealed record ResourceRow(
        Guid resource_id,
        string type,
        string status,
        string title,
        string? summary,
        string? body,
        string? external_url);

    private sealed record ResourceTopicRow(Guid topic_id, string code, string name);

    private sealed record ResourceSummaryRow(
        Guid resource_id,
        string type,
        string status,
        string title,
        string? summary,
        int topic_count,
        string[]? topic_codes);
}
