using System.Text.RegularExpressions;
using Yusay.Domain.Common;

namespace Yusay.Domain.Identity.ValueObjects;

public readonly record struct Email
{
    private static readonly Regex EmailRegex = new(
        @"^[a-zA-Z0-9._+-]+@[a-zA-Z0-9.-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const int MaxLength = 254;

    public string Value { get; }

    private Email(string value)
    {
        Value = value;
    }

    public static Email Create(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("El correo electrónico no puede estar vacío.");
        }

        string trimmed = email.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new DomainException($"El correo electrónico no puede superar los {MaxLength} caracteres.");
        }

        if (trimmed.StartsWith('.') || trimmed.EndsWith('.') || trimmed.Contains("..") || trimmed.Contains(".@") || trimmed.Contains("@."))
        {
            throw new DomainException("El correo electrónico contiene una sintaxis inválida de puntos.");
        }

        int atIndex = trimmed.IndexOf('@');
        if (atIndex <= 0 || atIndex != trimmed.LastIndexOf('@') || atIndex == trimmed.Length - 1)
        {
            throw new DomainException("El correo electrónico debe contener exactamente un delimitador '@' válido.");
        }

        if (!EmailRegex.IsMatch(trimmed))
        {
            throw new DomainException("El formato del correo electrónico no es válido según la especificación RFC 5321.");
        }

        string canonical = trimmed.ToLowerInvariant();

        return new Email(canonical);
    }

    public override string ToString() => Value;

    public static implicit operator string(Email email) => email.Value;
    public static explicit operator Email(string email) => Create(email);
}
