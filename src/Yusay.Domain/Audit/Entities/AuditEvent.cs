using Yusay.Domain.Common;

namespace Yusay.Domain.Audit.Entities;

public sealed class AuditEvent
{
    public Guid AuditEventId { get; }
    public Guid? ActorUserId { get; }
    public string ActorKind { get; }
    public string Action { get; }
    public string TargetType { get; }
    public string? TargetIdentifier { get; }
    public DateTimeOffset OccurredAt { get; }
    public string? Metadata { get; }

    private AuditEvent(
        Guid auditEventId,
        Guid? actorUserId,
        string actorKind,
        string action,
        string targetType,
        string? targetIdentifier,
        DateTimeOffset occurredAt,
        string? metadata)
    {
        AuditEventId = auditEventId;
        ActorUserId = actorUserId;
        ActorKind = actorKind;
        Action = action;
        TargetType = targetType;
        TargetIdentifier = targetIdentifier;
        OccurredAt = occurredAt;
        Metadata = metadata;
    }

    public static AuditEvent CreateUserRegistered(
        Guid userId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador del usuario para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: userId,
            actorKind: "USER",
            action: "USER_REGISTERED",
            targetType: "USER",
            targetIdentifier: userId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateEmailVerificationTokenIssued(
        Guid userId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador del usuario para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: userId,
            actorKind: "USER",
            action: "EMAIL_VERIFICATION_TOKEN_ISSUED",
            targetType: "USER",
            targetIdentifier: userId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateEmailVerified(
        Guid userId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador del usuario para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: userId,
            actorKind: "USER",
            action: "EMAIL_VERIFIED",
            targetType: "USER",
            targetIdentifier: userId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreatePasswordResetTokenIssued(
        Guid userId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador del usuario para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: userId,
            actorKind: "USER",
            action: "PASSWORD_RESET_TOKEN_ISSUED",
            targetType: "USER",
            targetIdentifier: userId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreatePasswordResetCompleted(
        Guid userId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El identificador del usuario para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: userId,
            actorKind: "USER",
            action: "PASSWORD_RESET_COMPLETED",
            targetType: "USER",
            targetIdentifier: userId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent Rehydrate(
        Guid auditEventId,
        Guid? actorUserId,
        string actorKind,
        string action,
        string targetType,
        string? targetIdentifier,
        DateTimeOffset occurredAt,
        string? metadata)
    {
        return new AuditEvent(
            auditEventId,
            actorUserId,
            actorKind,
            action,
            targetType,
            targetIdentifier,
            occurredAt,
            metadata);
    }
}
