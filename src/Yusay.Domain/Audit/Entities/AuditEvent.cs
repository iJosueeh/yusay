using System.Text.Json;
using Yusay.Domain.Common;

namespace Yusay.Domain.Audit.Entities;

public sealed class AuditEvent
{
    private const string SignInFailedMetadata = """{"reason_code":"CREDENTIALS_NOT_ACCEPTED"}""";

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

    public static AuditEvent CreateSignInSucceeded(
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
            action: "SIGN_IN_SUCCEEDED",
            targetType: "AUTHENTICATION",
            targetIdentifier: null,
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateSignInFailed(
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: null,
            actorKind: "ANONYMOUS",
            action: "SIGN_IN_FAILED",
            targetType: "AUTHENTICATION",
            targetIdentifier: null,
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: SignInFailedMetadata);
    }

    public static AuditEvent CreateSignOut(
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
            action: "SIGN_OUT",
            targetType: "AUTHENTICATION",
            targetIdentifier: null,
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    private static readonly IReadOnlyCollection<string> DimensionCatalogChangedFields =
        new HashSet<string>(StringComparer.Ordinal) { "name", "description" };

    private static readonly IReadOnlyCollection<string> InstrumentCatalogChangedFields =
        new HashSet<string>(StringComparer.Ordinal) { "name", "description", "purpose" };

    public static AuditEvent CreateInstrumentCreated(
        Guid instrumentId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentId == Guid.Empty)
        {
            throw new DomainException("El identificador del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_CREATED",
            targetType: "INSTRUMENT",
            targetIdentifier: instrumentId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateInstrumentUpdated(
        Guid instrumentId,
        Guid administratorUserId,
        IReadOnlyList<string> changedFields,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentId == Guid.Empty)
        {
            throw new DomainException("El identificador del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        if (changedFields is null || changedFields.Count == 0)
        {
            throw new DomainException(
                "La lista de campos modificados (changed_fields) de una edición de catálogo no puede estar vacía.");
        }

        foreach (var field in changedFields)
        {
            if (!InstrumentCatalogChangedFields.Contains(field))
            {
                throw new DomainException(
                    $"El campo '{field}' no está permitido en changed_fields de una edición de INSTRUMENT (lista cerrada: name, description, purpose).");
            }
        }

        var metadata = JsonSerializer.Serialize(new { changed_fields = changedFields });

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_UPDATED",
            targetType: "INSTRUMENT",
            targetIdentifier: instrumentId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: metadata);
    }

    public static AuditEvent CreateInstrumentVersionCreated(
        Guid instrumentVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_CREATED",
            targetType: "INSTRUMENT_VERSION",
            targetIdentifier: instrumentVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    private static readonly IReadOnlyCollection<string> InstrumentVersionComponentCategories =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "REFERENCES",
            "QUESTIONS",
            "ANSWER_OPTIONS",
            "SCORING",
            "INTERPRETATIONS"
        };

    public static AuditEvent CreateInstrumentVersionUpdated(
        Guid instrumentVersionId,
        Guid administratorUserId,
        IReadOnlyList<string> changedCategories,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        if (changedCategories is null || changedCategories.Count == 0)
        {
            throw new DomainException("La lista de categorías modificadas no puede estar vacía.");
        }

        foreach (var category in changedCategories)
        {
            if (!InstrumentVersionComponentCategories.Contains(category))
            {
                throw new DomainException(
                    $"La categoría '{category}' no está permitida en changed_fields de una edición de INSTRUMENT_VERSION (lista cerrada: REFERENCES, QUESTIONS, ANSWER_OPTIONS, SCORING, INTERPRETATIONS).");
            }
        }

        var metadata = JsonSerializer.Serialize(new { changed_fields = changedCategories });

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_UPDATED",
            targetType: "INSTRUMENT_VERSION",
            targetIdentifier: instrumentVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: metadata);
    }

    public static AuditEvent CreateInstrumentVersionReady(
        Guid instrumentVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "INSTRUMENT_VERSION_READY",
            targetType: "INSTRUMENT_VERSION",
            targetIdentifier: instrumentVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateInstrumentVersionReturnedToDraft(
        Guid instrumentVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "INSTRUMENT_VERSION_RETURNED_TO_DRAFT",
            targetType: "INSTRUMENT_VERSION",
            targetIdentifier: instrumentVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateInstrumentVersionPublished(
        Guid instrumentVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "INSTRUMENT_VERSION_PUBLISHED",
            targetType: "INSTRUMENT_VERSION",
            targetIdentifier: instrumentVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateInstrumentVersionRetired(
        Guid instrumentVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión del instrumento para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "INSTRUMENT_VERSION_RETIRED",
            targetType: "INSTRUMENT_VERSION",
            targetIdentifier: instrumentVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateCatalogCreated(
        Guid dimensionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (dimensionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la dimensión para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_CREATED",
            targetType: "DIMENSION",
            targetIdentifier: dimensionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateCatalogUpdated(
        Guid dimensionId,
        Guid administratorUserId,
        IReadOnlyList<string> changedFields,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (dimensionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la dimensión para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        if (changedFields is null || changedFields.Count == 0)
        {
            throw new DomainException(
                "La lista de campos modificados (changed_fields) de una edición de catálogo no puede estar vacía.");
        }

        foreach (var field in changedFields)
        {
            if (!DimensionCatalogChangedFields.Contains(field))
            {
                throw new DomainException(
                    $"El campo '{field}' no está permitido en changed_fields de una edición de DIMENSION (lista cerrada: name, description).");
            }
        }

        var metadata = JsonSerializer.Serialize(new { changed_fields = changedFields });

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_UPDATED",
            targetType: "DIMENSION",
            targetIdentifier: dimensionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: metadata);
    }

    private static readonly IReadOnlyCollection<string> DimensionVersionChangedFields =
        new HashSet<string>(StringComparer.Ordinal) { "definition", "min_value", "max_value", "step" };

    public static AuditEvent CreateCatalogVersionCreated(
        Guid dimensionVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (dimensionVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_CREATED",
            targetType: "DIMENSION_VERSION",
            targetIdentifier: dimensionVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateCatalogVersionUpdated(
        Guid dimensionVersionId,
        Guid administratorUserId,
        IReadOnlyList<string>? changedFields,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (dimensionVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        string? metadata = null;
        if (changedFields is { Count: > 0 })
        {
            foreach (var field in changedFields)
            {
                if (!DimensionVersionChangedFields.Contains(field))
                {
                    throw new DomainException(
                        $"El campo '{field}' no está permitido en changed_fields de una edición de DIMENSION_VERSION (lista cerrada: definition, min_value, max_value, step).");
                }
            }

            metadata = JsonSerializer.Serialize(new { changed_fields = changedFields });
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "CATALOG_UPDATED",
            targetType: "DIMENSION_VERSION",
            targetIdentifier: dimensionVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: metadata);
    }

    public static AuditEvent CreateCatalogVersionActivated(
        Guid dimensionVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (dimensionVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "DIMENSION_VERSION_ACTIVATED",
            targetType: "DIMENSION_VERSION",
            targetIdentifier: dimensionVersionId.ToString(),
            occurredAt: occurredAt ?? DateTimeOffset.UtcNow,
            metadata: null);
    }

    public static AuditEvent CreateCatalogVersionRetired(
        Guid dimensionVersionId,
        Guid administratorUserId,
        DateTimeOffset? occurredAt = null,
        Guid? auditEventId = null)
    {
        if (dimensionVersionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la versión para auditoría no puede ser vacío.");
        }

        if (administratorUserId == Guid.Empty)
        {
            throw new DomainException("El identificador del administrador para auditoría no puede ser vacío.");
        }

        return new AuditEvent(
            auditEventId: auditEventId ?? Guid.NewGuid(),
            actorUserId: administratorUserId,
            actorKind: "ADMINISTRATOR",
            action: "DIMENSION_VERSION_RETIRED",
            targetType: "DIMENSION_VERSION",
            targetIdentifier: dimensionVersionId.ToString(),
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
