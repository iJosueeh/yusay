using System.Globalization;
using System.Text;
using Yusay.Application.Common.Exceptions;

namespace Yusay.Application.Content.Queries.ListResources;

public static class ResourceListCursor
{
    private const int Version = 1;
    private const int MaxLength = 512;
    private const string InvalidMessage = "El cursor proporcionado no es válido.";

    public sealed record CursorKey(string Title, Guid ResourceId);

    public static string Encode(string title, Guid resourceId)
    {
        var plaintext = string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1}|{2}",
            Version,
            title,
            resourceId.ToString("D"));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static CursorKey? Decode(string? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        if (cursor.Length == 0 || cursor.Length > MaxLength)
        {
            throw new ValidationException(InvalidMessage);
        }

        if (!TryFromBase64Url(cursor, out var bytes))
        {
            throw new ValidationException(InvalidMessage);
        }

        string plaintext;
        try
        {
            plaintext = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ValidationException(InvalidMessage);
        }

        // Dividimos respetando que el título puede contener '|'
        var firstPipe = plaintext.IndexOf('|');
        var lastPipe = plaintext.LastIndexOf('|');

        if (firstPipe <= 0 || lastPipe <= firstPipe || lastPipe == plaintext.Length - 1)
        {
            throw new ValidationException(InvalidMessage);
        }

        var versionStr = plaintext[..firstPipe];
        if (versionStr != Version.ToString(CultureInfo.InvariantCulture))
        {
            throw new ValidationException(InvalidMessage);
        }

        var title = plaintext.Substring(firstPipe + 1, lastPipe - firstPipe - 1);
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ValidationException(InvalidMessage);
        }

        var idStr = plaintext[(lastPipe + 1)..];

        if (!Guid.TryParseExact(idStr, "D", out var resourceId))
        {
            throw new ValidationException(InvalidMessage);
        }

        return new CursorKey(title, resourceId);
    }

    private static bool TryFromBase64Url(string value, out byte[] bytes)
    {
        bytes = [];
        foreach (var character in value)
        {
            if (!IsBase64UrlCharacter(character))
            {
                return false;
            }
        }

        var padded = value.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            1 => padded + "===",
            2 => padded + "==",
            3 => padded + "=",
            _ => padded
        };

        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsBase64UrlCharacter(char character) =>
        character is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
}
