using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class Question
{
    public Guid QuestionId { get; }
    public Guid InstrumentVersionId { get; }
    public int Position { get; }
    public string Prompt { get; }
    public bool Required { get; }

    public Question(
        Guid questionId,
        Guid instrumentVersionId,
        int position,
        string prompt,
        bool required = true)
    {
        if (questionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la pregunta no puede ser un UUID vacío.");
        }

        if (instrumentVersionId == Guid.Empty)
        {
            throw new DomainException("La pregunta debe pertenecer a una versión válida (UUID no vacío).");
        }

        if (position <= 0)
        {
            throw new DomainException($"La posición de la pregunta debe ser mayor a cero (recibido: {position}).");
        }

        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new DomainException("El enunciado de la pregunta no puede estar vacío.");
        }

        QuestionId = questionId;
        InstrumentVersionId = instrumentVersionId;
        Position = position;
        Prompt = prompt.Trim();
        Required = required;
    }

    public static Question Create(
        Guid instrumentVersionId,
        int position,
        string prompt,
        bool required = true,
        Guid? questionId = null) =>
        new(
            questionId ?? Guid.NewGuid(),
            instrumentVersionId,
            position,
            prompt,
            required);
}
