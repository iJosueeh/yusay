using System.Text.RegularExpressions;
using Yusay.Domain.Common;

namespace Yusay.Domain.Tracking.ValueObjects;

public readonly record struct DimensionCode
{
    private static readonly Regex CodeRegex = new(
        @"^[a-zA-Z0-9_-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public string Value { get; }

    private DimensionCode(string value)
    {
        Value = value;
    }

    public static DimensionCode Create(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            throw new DomainException("El código de la dimensión no puede ser nulo ni vacío.");
        }

        if (!CodeRegex.IsMatch(code))
        {
            throw new DomainException(
                $"El código de la dimensión '{code}' no es válido. Solo se permiten letras, números, guiones y guiones bajos (^[a-zA-Z0-9_-]+$).");
        }

        return new DimensionCode(code);
    }

    public override string ToString() => Value ?? string.Empty;

    public static implicit operator string(DimensionCode code) => code.Value;
    public static explicit operator DimensionCode(string code) => Create(code);
}
