using Yusay.Domain.Common;
using Yusay.Domain.Content.Enums;

namespace Yusay.Domain.Content.Entities;

public sealed class Resource
{
    private readonly HashSet<Guid> _topicIds;

    public Guid ResourceId { get; }
    public ResourceType Type { get; private set; }
    public ResourceStatus Status { get; private set; }
    public string Title { get; private set; }
    public string? Summary { get; private set; }
    public string? Body { get; private set; }
    public string? ExternalUrl { get; private set; }

    public IReadOnlyCollection<Guid> TopicIds => _topicIds;

    private Resource(
        Guid resourceId,
        ResourceType type,
        ResourceStatus status,
        string title,
        string? summary,
        string? body,
        string? externalUrl,
        IEnumerable<Guid>? topicIds)
    {
        ResourceId = resourceId;
        Type = type;
        Status = status;
        Title = title;
        Summary = summary;
        Body = body;
        ExternalUrl = externalUrl;
        _topicIds = topicIds != null ? [.. topicIds] : [];
    }

    public static Resource CreateDraft(
        ResourceType type,
        string title,
        string? summary = null,
        string? body = null,
        string? externalUrl = null,
        IEnumerable<Guid>? topicIds = null,
        Guid? resourceId = null)
    {
        var id = resourceId ?? Guid.NewGuid();
        if (id == Guid.Empty)
        {
            throw new DomainException("El identificador del recurso no puede ser un UUID vacío.");
        }

        ValidateTitle(title);

        var (cleanSummary, cleanBody, cleanUrl) = NormalizeFields(summary, body, externalUrl);

        return new Resource(
            id,
            type,
            ResourceStatus.Draft,
            title.Trim(),
            cleanSummary,
            cleanBody,
            cleanUrl,
            topicIds);
    }

    public static Resource Rehydrate(
        Guid resourceId,
        ResourceType type,
        ResourceStatus status,
        string title,
        string? summary,
        string? body,
        string? externalUrl,
        IEnumerable<Guid>? topicIds)
    {
        if (resourceId == Guid.Empty)
        {
            throw new DomainException("El identificador del recurso no puede ser un UUID vacío.");
        }

        ValidateTitle(title);

        var (cleanSummary, cleanBody, cleanUrl) = NormalizeFields(summary, body, externalUrl);

        return new Resource(
            resourceId,
            type,
            status,
            title.Trim(),
            cleanSummary,
            cleanBody,
            cleanUrl,
            topicIds);
    }

    public void UpdateDraft(
        ResourceType type,
        string title,
        string? summary,
        string? body,
        string? externalUrl)
    {
        if (Status != ResourceStatus.Draft)
        {
            throw new DomainException(
                $"No se puede editar libremente un recurso en estado {Status.ToDbValue()}. Solo los borradores permiten edición completa.");
        }

        ValidateTitle(title);
        var (cleanSummary, cleanBody, cleanUrl) = NormalizeFields(summary, body, externalUrl);

        Type = type;
        Title = title.Trim();
        Summary = cleanSummary;
        Body = cleanBody;
        ExternalUrl = cleanUrl;
    }

    public void ApplyMinorCorrection(
        string title,
        string? summary,
        string? body,
        string? externalUrl,
        CorrectionKind correctionKind)
    {
        if (Status != ResourceStatus.Published)
        {
            throw new DomainException(
                $"Las correcciones editoriales menores solo aplican a recursos en estado PUBLISHED. El estado actual es {Status.ToDbValue()}.");
        }

        ValidateTitle(title);
        var (cleanSummary, cleanBody, cleanUrl) = NormalizeFields(summary, body, externalUrl);

        // Validar condiciones de tipo para recursos publicados
        ValidatePublishedContent(Type, cleanBody, cleanUrl);

        Title = title.Trim();
        Summary = cleanSummary;
        Body = cleanBody;
        ExternalUrl = cleanUrl;
    }

    public void Publish()
    {
        if (Status == ResourceStatus.Published)
        {
            return;
        }

        if (Status != ResourceStatus.Draft)
        {
            throw new DomainException(
                $"Transición no permitida: no se puede publicar un recurso en estado {Status.ToDbValue()}. Solo DRAFT puede pasar a PUBLISHED.");
        }

        ValidateTitle(Title);
        ValidatePublishedContent(Type, Body, ExternalUrl);

        if (_topicIds.Count == 0)
        {
            throw new DomainException("Un recurso no puede publicarse sin tener al menos un tema asociado.");
        }

        Status = ResourceStatus.Published;
    }

    public void Retire()
    {
        if (Status == ResourceStatus.Retired)
        {
            return;
        }

        if (Status != ResourceStatus.Published)
        {
            throw new DomainException(
                $"Transición no permitida: solo se puede retirar un recurso en estado PUBLISHED. No se permite retirar directamente desde {Status.ToDbValue()}.");
        }

        Status = ResourceStatus.Retired;
    }

    public void AddTopic(Guid topicId)
    {
        if (topicId == Guid.Empty)
        {
            throw new DomainException("El identificador del tema no puede ser vacío.");
        }

        if (Status == ResourceStatus.Retired)
        {
            throw new DomainException("No se pueden modificar los temas de un recurso retirado.");
        }

        _topicIds.Add(topicId);
    }

    public void RemoveTopic(Guid topicId)
    {
        if (topicId == Guid.Empty)
        {
            throw new DomainException("El identificador del tema no puede ser vacío.");
        }

        if (Status == ResourceStatus.Retired)
        {
            throw new DomainException("No se pueden modificar los temas de un recurso retirado.");
        }

        if (Status == ResourceStatus.Published && _topicIds.Count == 1 && _topicIds.Contains(topicId))
        {
            throw new DomainException(
                "No se puede desvincular el único tema de un recurso publicado. Debe mantener al menos un tema asociado.");
        }

        _topicIds.Remove(topicId);
    }

    public static void ValidatePublishedContent(ResourceType type, string? body, string? externalUrl)
    {
        if (type == ResourceType.Article)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                throw new DomainException("Un recurso de tipo ARTICLE publicado requiere un cuerpo (body) no vacío.");
            }

            if (!string.IsNullOrWhiteSpace(externalUrl))
            {
                throw new DomainException("Un recurso de tipo ARTICLE publicado no debe tener external_url.");
            }
        }
        else if (type == ResourceType.ExternalLink)
        {
            if (!string.IsNullOrWhiteSpace(body))
            {
                throw new DomainException("Un recurso de tipo EXTERNAL_LINK publicado no debe tener cuerpo (body).");
            }

            if (string.IsNullOrWhiteSpace(externalUrl))
            {
                throw new DomainException("Un recurso de tipo EXTERNAL_LINK publicado requiere una URL externa obligatoria.");
            }

            var trimmedUrl = externalUrl.Trim();
            if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new DomainException(
                    "La URL externa de un recurso EXTERNAL_LINK publicado debe ser una URL absoluta válida con protocolo HTTPS.");
            }
        }
    }

    private static void ValidateTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("El título del recurso no puede estar vacío ni contener solo espacios.");
        }

        if (title.Trim().Length > 256)
        {
            throw new DomainException("El título del recurso no puede exceder los 256 caracteres.");
        }
    }

    private static (string? cleanSummary, string? cleanBody, string? cleanUrl) NormalizeFields(
        string? summary,
        string? body,
        string? externalUrl)
    {
        var cleanSummary = string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();
        var cleanBody = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
        var cleanUrl = string.IsNullOrWhiteSpace(externalUrl) ? null : externalUrl.Trim();

        if (cleanSummary != null && cleanSummary.Length > 1024)
        {
            throw new DomainException("El resumen del recurso no puede superar los 1024 caracteres.");
        }

        if (cleanUrl != null && cleanUrl.Length > 2048)
        {
            throw new DomainException("La URL externa del recurso no puede superar los 2048 caracteres.");
        }

        return (cleanSummary, cleanBody, cleanUrl);
    }
}
