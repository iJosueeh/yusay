using System.Data.Common;
using Dapper;
using Npgsql;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Content.Repositories;
using Yusay.Domain.Content.Entities;
using Yusay.Domain.Content.ValueObjects;

namespace Yusay.Infrastructure.Content.Repositories;

public sealed class TopicRepository(IDbConnectionFactory connectionFactory) : ITopicRepository
{
    private const string DuplicateCodeConstraintName = "uq_topic_code";

    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT EXISTS (SELECT 1 FROM yusay.topic WHERE code = @Code);";

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(sql, new { Code = code }, cancellationToken: cancellationToken));
    }

    public async Task<Topic?> GetByIdAsync(Guid topicId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT topic_id, code, name, description
            FROM yusay.topic
            WHERE topic_id = @TopicId;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<TopicRow>(
            new CommandDefinition(sql, new { TopicId = topicId }, cancellationToken: cancellationToken));

        return row is null ? null : MapTopic(row);
    }

    public async Task<IReadOnlyList<Topic>> ListAllAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT topic_id, code, name, description
            FROM yusay.topic
            ORDER BY code ASC;
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<TopicRow>(
            new CommandDefinition(sql, cancellationToken: cancellationToken));

        return rows.Select(MapTopic).ToList();
    }

    public async Task AddAsync(
        Topic topic,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.topic (topic_id, code, name, description)
            VALUES (@TopicId, @Code, @Name, @Description);
            """;

        var parameters = new
        {
            topic.TopicId,
            Code = topic.Code.Value,
            topic.Name,
            topic.Description
        };

        try
        {
            if (transaction?.Connection != null)
            {
                var command = new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken);
                await transaction.Connection.ExecuteAsync(command);
                return;
            }

            await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var directCommand = new CommandDefinition(sql, parameters, cancellationToken: cancellationToken);
            await connection.ExecuteAsync(directCommand);
        }
        catch (PostgresException ex) when (ex.ConstraintName == DuplicateCodeConstraintName)
        {
            throw new ConflictException(ITopicRepository.DuplicateCodeConflictMessage);
        }
    }

    public async Task<bool> AllTopicsExistAsync(
        IEnumerable<Guid> topicIds,
        CancellationToken cancellationToken = default)
    {
        var idList = topicIds.Distinct().ToArray();
        if (idList.Length == 0)
        {
            return true;
        }

        const string sql = """
            SELECT COUNT(DISTINCT topic_id)
            FROM yusay.topic
            WHERE topic_id = ANY(@TopicIds);
            """;

        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(sql, new { TopicIds = idList }, cancellationToken: cancellationToken));

        return count == idList.Length;
    }

    private static Topic MapTopic(TopicRow row) =>
        Topic.Rehydrate(row.topic_id, TopicCode.Create(row.code), row.name, row.description);

    private sealed record TopicRow(Guid topic_id, string code, string name, string? description);
}
