using System.Data.Common;
using Dapper;
using Yusay.Application.Common.Interfaces;
using Yusay.Domain.Audit.Entities;

namespace Yusay.Infrastructure.Audit.Repositories;

public sealed class AuditEventRepository(IDbConnectionFactory connectionFactory) : IAuditEventRepository
{
    private readonly IDbConnectionFactory _connectionFactory = connectionFactory;

    public async Task AddAsync(
        AuditEvent auditEvent,
        DbTransaction? transaction = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO yusay.audit_event (
                audit_event_id,
                actor_user_id,
                actor_kind,
                action,
                target_type,
                target_identifier,
                occurred_at,
                metadata
            ) VALUES (
                @AuditEventId,
                @ActorUserId,
                @ActorKind,
                @Action,
                @TargetType,
                @TargetIdentifier,
                @OccurredAt,
                CAST(@Metadata AS jsonb)
            );
            """;

        var parameters = new
        {
            auditEvent.AuditEventId,
            auditEvent.ActorUserId,
            auditEvent.ActorKind,
            auditEvent.Action,
            auditEvent.TargetType,
            auditEvent.TargetIdentifier,
            auditEvent.OccurredAt,
            auditEvent.Metadata
        };

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
}
