using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class ScoringContribution
{
    public Guid OptionId { get; }
    public Guid QuestionId { get; }
    public Guid InstrumentVersionId { get; }
    public int Contribution { get; }

    public ScoringContribution(
        Guid optionId,
        Guid questionId,
        Guid instrumentVersionId,
        int contribution)
    {
        if (optionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la opción para la contribución no puede ser un UUID vacío.");
        }

        if (questionId == Guid.Empty)
        {
            throw new DomainException("La pregunta para la contribución no puede ser un UUID vacío.");
        }

        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("La versión del instrumento para la contribución no puede ser un UUID vacío.");
        }

        OptionId = optionId;
        QuestionId = questionId;
        InstrumentVersionId = instrumentVersionId;
        Contribution = contribution;
    }
}
