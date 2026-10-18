using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.Entities;

public sealed class AnswerOption
{
    public Guid OptionId { get; }
    public Guid QuestionId { get; }
    public int Position { get; }
    public string Label { get; }

    public AnswerOption(
        Guid optionId,
        Guid questionId,
        int position,
        string label)
    {
        if (optionId == Guid.Empty)
        {
            throw new DomainException("El identificador de la opción no puede ser un UUID vacío.");
        }

        if (questionId == Guid.Empty)
        {
            throw new DomainException("La opción debe pertenecer a una pregunta válida (UUID no vacío).");
        }

        if (position <= 0)
        {
            throw new DomainException($"La posición de la opción debe ser mayor a cero (recibido: {position}).");
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            throw new DomainException("La etiqueta de la opción no puede estar vacía.");
        }

        OptionId = optionId;
        QuestionId = questionId;
        Position = position;
        Label = label.Trim();
    }

    public static AnswerOption Create(
        Guid questionId,
        int position,
        string label,
        Guid? optionId = null) =>
        new(
            optionId ?? Guid.NewGuid(),
            questionId,
            position,
            label);
}
