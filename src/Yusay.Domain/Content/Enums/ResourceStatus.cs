namespace Yusay.Domain.Content.Enums;

public enum ResourceStatus
{
    Draft,
    Published,
    Retired
}

public static class ResourceStatusExtensions
{
    public static string ToDbValue(this ResourceStatus status) =>
        status switch
        {
            ResourceStatus.Draft => "DRAFT",
            ResourceStatus.Published => "PUBLISHED",
            ResourceStatus.Retired => "RETIRED",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de recurso no reconocido.")
        };

    public static ResourceStatus FromDbValue(string value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "DRAFT" => ResourceStatus.Draft,
            "PUBLISHED" => ResourceStatus.Published,
            "RETIRED" => ResourceStatus.Retired,
            _ => throw new ArgumentException($"Estado de recurso no válido: '{value}'.", nameof(value))
        };
}
