namespace Yusay.Domain.Content.Enums;

public enum CorrectionKind
{
    Spelling,
    Punctuation,
    Format,
    SurfaceClarity,
    SameContentLinkRepair
}

public static class CorrectionKindExtensions
{
    public static string ToDbValue(this CorrectionKind kind) =>
        kind switch
        {
            CorrectionKind.Spelling => "SPELLING",
            CorrectionKind.Punctuation => "PUNCTUATION",
            CorrectionKind.Format => "FORMAT",
            CorrectionKind.SurfaceClarity => "SURFACE_CLARITY",
            CorrectionKind.SameContentLinkRepair => "SAME_CONTENT_LINK_REPAIR",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Tipo de corrección no reconocido.")
        };

    public static CorrectionKind FromDbValue(string value) =>
        value?.Trim().ToUpperInvariant() switch
        {
            "SPELLING" => CorrectionKind.Spelling,
            "PUNCTUATION" => CorrectionKind.Punctuation,
            "FORMAT" => CorrectionKind.Format,
            "SURFACE_CLARITY" => CorrectionKind.SurfaceClarity,
            "SAME_CONTENT_LINK_REPAIR" => CorrectionKind.SameContentLinkRepair,
            _ => throw new ArgumentException($"Tipo de corrección editorial no válido: '{value}'.", nameof(value))
        };
}
