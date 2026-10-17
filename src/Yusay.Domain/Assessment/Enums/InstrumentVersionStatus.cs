namespace Yusay.Domain.Assessment.Enums;

public enum InstrumentVersionStatus
{
    Draft,
    Ready,
    Published,
    Retired
}

public static class InstrumentVersionStatusExtensions
{
    public static string ToDbValue(this InstrumentVersionStatus status) =>
        status switch
        {
            InstrumentVersionStatus.Draft => "DRAFT",
            InstrumentVersionStatus.Ready => "READY",
            InstrumentVersionStatus.Published => "PUBLISHED",
            InstrumentVersionStatus.Retired => "RETIRED",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Estado de versión de instrumento no reconocido.")
        };

    public static InstrumentVersionStatus FromDbValue(string value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "DRAFT" => InstrumentVersionStatus.Draft,
            "READY" => InstrumentVersionStatus.Ready,
            "PUBLISHED" => InstrumentVersionStatus.Published,
            "RETIRED" => InstrumentVersionStatus.Retired,
            _ => throw new ArgumentException($"Estado de versión de instrumento no válido: '{value}'.", nameof(value))
        };
}
