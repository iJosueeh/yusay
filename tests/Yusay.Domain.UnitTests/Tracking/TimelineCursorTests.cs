using System.Globalization;
using System.Text;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Queries.GetTimeline;

namespace Yusay.Domain.UnitTests.Tracking;

public class TimelineCursorTests
{
    private static readonly DateTimeOffset Sample =
        new DateTimeOffset(2026, 10, 18, 12, 30, 45, TimeSpan.Zero).AddTicks(1234567);

    private const string InvalidMessage = "El cursor proporcionado no es válido.";
    private const string SampleGuid = "550e8400-e29b-41d4-a716-446655440000";

    private static string ToBase64Url(string plaintext) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static string ToBase64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    [Theory]
    [InlineData("assessment")]
    [InlineData("check_in")]
    public void Encode_ThenDecode_ShouldRoundTrip(string eventType)
    {
        var encoded = TimelineCursor.Encode(Sample, eventType, Guid.Parse(SampleGuid));

        var key = TimelineCursor.Decode(encoded);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.OccurredAt);
        Assert.Equal(eventType, key.EventType);
        Assert.Equal(Guid.Parse(SampleGuid), key.EventId);
        Assert.Equal(TimeSpan.Zero, key.OccurredAt.Offset);
    }

    [Fact]
    public void Encode_WithNonUtcOffset_ShouldNormalizeToUtc()
    {
        var encoded = TimelineCursor.Encode(Sample.ToOffset(TimeSpan.FromHours(5)), "assessment", Guid.Parse(SampleGuid));

        var key = TimelineCursor.Decode(encoded);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.OccurredAt);
        Assert.Equal(TimeSpan.Zero, key.OccurredAt.Offset);
    }

    [Fact]
    public void Encode_ShouldProduceUnpaddedBase64Url()
    {
        var encoded = TimelineCursor.Encode(Sample, "check_in", Guid.Parse(SampleGuid));

        Assert.NotEmpty(encoded);
        Assert.Matches("^[A-Za-z0-9_-]+$", encoded);
    }

    [Fact]
    public void Decode_WithoutCursor_ShouldReturnNull()
    {
        Assert.Null(TimelineCursor.Decode(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc+def/")]
    [InlineData("abcd")]
    [InlineData("YWJjZA==")]
    [InlineData("2|2026-10-18T12:30:45.1234567+00:00|assessment|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T12:30:45.1234567+00:00|assessment")]
    [InlineData("1|2026-10-18T12:30:45.1234567+00:00|assessment|550e8400-e29b-41d4-a716-446655440000|extra")]
    [InlineData("1|not-a-timestamp|assessment|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T12:30:45+00:00|assessment|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T12:30:45.1234567+02:00|assessment|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T12:30:45.1234567+00:00|unknown_type|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T12:30:45.1234567+00:00|assessment|not-a-guid")]
    public void Decode_WithMalformedPayload_ShouldThrowUniformValidation(string plaintext)
    {
        var cursor = plaintext.Length == 0 ? plaintext : ToBase64Url(plaintext);

        var exception = Assert.Throws<ValidationException>(() => TimelineCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("ab+c")]
    [InlineData("==")]
    public void Decode_WithInvalidAlphabet_ShouldThrowUniformValidation(string cursor)
    {
        var exception = Assert.Throws<ValidationException>(() => TimelineCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_WithOversizedCursor_ShouldThrowUniformValidation()
    {
        var oversized = new string('a', 257);

        var exception = Assert.Throws<ValidationException>(() => TimelineCursor.Decode(oversized));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_WithInvalidUtf8Sequence_ShouldThrowUniformValidation()
    {
        var invalidUtf8 = new byte[] { 0xC0, 0xAF };
        var cursor = ToBase64Url(invalidUtf8);

        var exception = Assert.Throws<ValidationException>(() => TimelineCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }
}
