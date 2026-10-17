using System.Text.RegularExpressions;
using Yusay.Domain.Common;

namespace Yusay.Domain.Assessment.ValueObjects;

public sealed record InstrumentCode
{
    private static readonly Regex CodePattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    public string Value { get; }

    private InstrumentCode(string value)
    {
        Value = value;
    }

    public static InstrumentCode Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException("El código del instrumento no puede estar vacío ni contener solo espacios.");
        }

        var trimmed = value.Trim();

        if (!CodePattern.IsMatch(trimmed))
        {
            throw new DomainException(
                $"El código '{trimmed}' no es válido. Solo se permiten caracteres alfanuméricos, guiones y guiones bajos (^[a-zA-Z0-9_-]+$).");
        }

        if (trimmed.Length > 64)
        {
            throw new DomainException("El código del instrumento no puede superar los 64 caracteres.");
        }

        return new InstrumentCode(trimmed);
    }

    public override string ToString() => Value;

    public static implicit operator string(InstrumentCode code) => code.Value;
}
