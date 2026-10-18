using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class AssessmentResult
{
    public Guid AttemptId { get; }
    public Guid InstrumentVersionId { get; }
    public int Score { get; }
    public Guid InterpretationId { get; }
    public DateTimeOffset CalculatedAt { get; }

    public AssessmentResult(
        Guid attemptId,
        Guid instrumentVersionId,
        int score,
        Guid interpretationId,
        DateTimeOffset calculatedAt)
    {
        if (attemptId == Guid.Empty)
        {
            throw new DomainException("El resultado debe estar asociado a un intento válido.");
        }

        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("El resultado debe estar asociado a una versión válida.");
        }

        if (interpretationId == Guid.Empty)
        {
            throw new DomainException("El resultado debe tener una interpretación válida.");
        }

        AttemptId = attemptId;
        InstrumentVersionId = instrumentVersionId;
        Score = score;
        InterpretationId = interpretationId;
        CalculatedAt = calculatedAt;
    }

    public static AssessmentResult Create(
        Guid attemptId,
        Guid instrumentVersionId,
        int score,
        Guid interpretationId,
        DateTimeOffset calculatedAt) =>
        new(attemptId, instrumentVersionId, score, interpretationId, calculatedAt);
}
