using System.Globalization;
using System.Text;
using Yusay.Application.Common.Exceptions;

namespace Yusay.Application.Tracking.Queries.GetTimeline;

public static class TimelineCursor
{
    private const int Version = 1;
    private const int MaxLength = 256;
    private const string InvalidMessage = "El cursor proporcionado no es válido.";

    public sealed record CursorKey(DateTimeOffset OccurredAt, string EventType, Guid EventId);

    public static string Encode(DateTimeOffset occurredAt, string eventType, Guid eventId)
    {
        var utc = occurredAt.ToUniversalTime();
        var plaintext = string.Format(
            CultureInfo.InvariantCulture,
            "{0}|{1}|{2}|{3}",
            Version,
            utc.ToString("O", CultureInfo.InvariantCulture),
            eventType,
            eventId.ToString("D"));

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
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new ValidationException(InvalidMessage);
        }

        var parts = plaintext.Split('|');
        if (parts.Length != 4 || parts[0] != Version.ToString(CultureInfo.InvariantCulture))
        {
            throw new ValidationException(InvalidMessage);
        }

        if (!DateTimeOffset.TryParseExact(
                parts[1],
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var occurredAt)
            || occurredAt.Offset != TimeSpan.Zero)
        {
            throw new ValidationException(InvalidMessage);
        }

        var eventType = parts[2];
        if (eventType != "assessment" && eventType != "check_in")
        {
            throw new ValidationException(InvalidMessage);
        }

        if (!Guid.TryParseExact(parts[3], "D", out var eventId))
        {
            throw new ValidationException(InvalidMessage);
        }

        return new CursorKey(occurredAt, eventType, eventId);
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
        (character >= 'A' && character <= 'Z')
        || (character >= 'a' && character <= 'z')
        || (character >= '0' && character <= '9')
        || character == '-'
        || character == '_';
}
