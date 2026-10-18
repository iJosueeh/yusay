using Yusay.Domain.Assessment.Enums;
using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class AssessmentAttempt
{
    public const int AttemptValidityHours = 720;

    public Guid AttemptId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid InstrumentId { get; private set; }
    public Guid InstrumentVersionId { get; private set; }
    public AssessmentAttemptStatus Status { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    private AssessmentAttempt() { }

    public static AssessmentAttempt Create(
        Guid userId,
        Guid instrumentId,
        Guid instrumentVersionId,
        DateTimeOffset startedAt)
    {
        if (userId == Guid.Empty)
        {
            throw new DomainException("El intento debe estar asociado a un usuario válido.");
        }

        if (instrumentId == Guid.Empty)
        {
            throw new DomainException("El intento debe especificar un instrumento válido.");
        }

        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El intento debe especificar una versión de instrumento válida.");
        }

        var expiresAt = startedAt.AddHours(AttemptValidityHours);

        return new AssessmentAttempt
        {
            AttemptId = Guid.NewGuid(),
            UserId = userId,
            InstrumentId = instrumentId,
            InstrumentVersionId = instrumentVersionId,
            Status = AssessmentAttemptStatus.InProgress,
            StartedAt = startedAt,
            ExpiresAt = expiresAt,
            EndedAt = null
        };
    }

    public static AssessmentAttempt Rehydrate(
        Guid attemptId,
        Guid userId,
        Guid instrumentId,
        Guid instrumentVersionId,
        AssessmentAttemptStatus status,
        DateTimeOffset startedAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? endedAt)
    {
        if (status == AssessmentAttemptStatus.InProgress && endedAt is not null)
        {
            throw new DomainException("Un intento en estado IN_PROGRESS no puede tener fecha de finalización.");
        }

        if (status != AssessmentAttemptStatus.InProgress && endedAt is null)
        {
            throw new DomainException("Un intento finalizado o expirado debe tener fecha de finalización.");
        }

        if (endedAt is not null && endedAt < startedAt)
        {
            throw new DomainException("La fecha de finalización no puede ser anterior a la de inicio.");
        }

        return new AssessmentAttempt
        {
            AttemptId = attemptId,
            UserId = userId,
            InstrumentId = instrumentId,
            InstrumentVersionId = instrumentVersionId,
            Status = status,
            StartedAt = startedAt,
            ExpiresAt = expiresAt,
            EndedAt = endedAt
        };
    }

    public void Expire(DateTimeOffset currentInstant)
    {
        if (Status != AssessmentAttemptStatus.InProgress)
        {
            throw new DomainException("Solo se puede expirar un intento que esté en estado IN_PROGRESS.");
        }

        if (currentInstant < ExpiresAt)
        {
            throw new DomainException("No se puede expirar un intento cuya fecha límite aún no ha vencido.");
        }

        Status = AssessmentAttemptStatus.Expired;
        EndedAt = ExpiresAt;
    }
}
