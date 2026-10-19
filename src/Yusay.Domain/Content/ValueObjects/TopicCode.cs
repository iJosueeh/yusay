using System.Text.RegularExpressions;
using Yusay.Domain.Common;

namespace Yusay.Domain.Content.ValueObjects;

public sealed partial record TopicCode
{
    private static readonly Regex CodePattern = GenerateCodeRegex();

    public string Value { get; }

    private TopicCode(string value)
    {
        Value = value;
    }

    public static TopicCode Create(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new DomainException("El código del tema no puede estar vacío ni contener solo espacios.");
        }

        var trimmed = code.Trim();

        if (trimmed.Length > 64)
        {
            throw new DomainException("El código del tema no puede superar los 64 caracteres.");
        }

        if (!CodePattern.IsMatch(trimmed))
        {
            throw new DomainException(
                "El código del tema solo puede contener letras, números, guiones y guiones bajos (^[a-zA-Z0-9_-]+$).");
        }

        return new TopicCode(trimmed);
    }

    [GeneratedRegex("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled)]
    private static partial Regex GenerateCodeRegex();
}
