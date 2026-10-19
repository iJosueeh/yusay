namespace Yusay.Domain.Content.Enums;

public enum ResourceType
{
    Article,
    ExternalLink
}

public static class ResourceTypeExtensions
{
    public static string ToDbValue(this ResourceType type) =>
        type switch
        {
            ResourceType.Article => "ARTICLE",
            ResourceType.ExternalLink => "EXTERNAL_LINK",
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Tipo de recurso no reconocido.")
        };

    public static ResourceType FromDbValue(string value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "ARTICLE" => ResourceType.Article,
            "EXTERNAL_LINK" => ResourceType.ExternalLink,
            _ => throw new ArgumentException($"Tipo de recurso no válido: '{value}'.", nameof(value))
        };
}
