using Yusay.Domain.Common;
using Yusay.Domain.Content.ValueObjects;

namespace Yusay.Domain.Content.Entities;

public sealed class Topic
{
    public Guid TopicId { get; }
    public TopicCode Code { get; }
    public string Name { get; private set; }
    public string? Description { get; private set; }

    private Topic(Guid topicId, TopicCode code, string name, string? description)
    {
        TopicId = topicId;
        Code = code;
        Name = name;
        Description = description;
    }

    public static Topic Create(TopicCode code, string name, string? description, Guid? topicId = null)
    {
        var id = topicId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador del tema no puede ser un UUID vacío.");
        }

        ValidateName(name);

        return new Topic(
            id,
            code,
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim());
    }

    public static Topic Rehydrate(Guid topicId, TopicCode code, string name, string? description)
    {
        if (topicId == Guid.Empty)
        {
            throw new DomainException("El identificador del tema no puede ser un UUID vacío.");
        }

        ValidateName(name);

        return new Topic(
            topicId,
            code,
            name.Trim(),
            string.IsNullOrWhiteSpace(description) ? null : description.Trim());
    }

    public void Update(string name, string? description)
    {
        ValidateName(name);
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("El nombre del tema no puede estar vacío ni contener solo espacios.");
        }

        if (name.Trim().Length > 128)
        {
            throw new DomainException("El nombre del tema no puede exceder los 128 caracteres.");
        }
    }
}
