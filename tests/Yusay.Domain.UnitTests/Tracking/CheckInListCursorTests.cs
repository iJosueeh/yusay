using System.Globalization;
using System.Text;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Tracking.Queries.ListCheckIns;

namespace Yusay.Domain.UnitTests.Tracking;

public class CheckInListCursorTests
{
    private static readonly DateTimeOffset Sample =
        new DateTimeOffset(2026, 10, 9, 15, 4, 5, TimeSpan.Zero).AddTicks(1234567);

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

    [Fact]
    public void Encode_ThenDecode_ShouldRoundTrip()
    {
        var encoded = CheckInListCursor.Encode(Sample, Guid.Parse(SampleGuid));

        var key = CheckInListCursor.Decode(encoded);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.RecordedAt);
        Assert.Equal(Guid.Parse(SampleGuid), key.CheckInId);
        Assert.Equal(TimeSpan.Zero, key.RecordedAt.Offset);
    }

    [Fact]
    public void Encode_WithNonUtcOffset_ShouldNormalizeToUtc()
    {
        var encoded = CheckInListCursor.Encode(Sample.ToOffset(TimeSpan.FromHours(2)), Guid.Parse(SampleGuid));

        var key = CheckInListCursor.Decode(encoded);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.RecordedAt);
    }

    [Fact]
    public void Encode_ShouldProduceUnpaddedBase64Url()
    {
        var encoded = CheckInListCursor.Encode(Sample, Guid.Parse(SampleGuid));

        Assert.NotEmpty(encoded);
        Assert.Matches("^[A-Za-z0-9_-]+$", encoded);
    }

    [Fact]
    public void Decode_WithoutCursor_ShouldReturnNull()
    {
        Assert.Null(CheckInListCursor.Decode(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc+def/")]
    [InlineData("abcd")]
    [InlineData("YWJjZA==")]
    [InlineData("2|2026-10-09T15:04:05.1234567+00:00|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-09T15:04:05.1234567+00:00")]
    [InlineData("1|2026-10-09T15:04:05.1234567+00:00|550e8400-e29b-41d4-a716-446655440000|x")]
    [InlineData("1|not-a-timestamp|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-09T15:04:05+00:00|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-09T15:04:05.1234567+02:00|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-09T15:04:05.1234567+00:00|not-a-guid")]
    public void Decode_WithMalformedPayload_ShouldThrowUniformValidation(string plaintext)
    {
        var cursor = plaintext.Length == 0 ? plaintext : ToBase64Url(plaintext);

        var exception = Assert.Throws<ValidationException>(() => CheckInListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("ab+c")]
    [InlineData("==")]
    public void Decode_WithInvalidAlphabet_ShouldThrowUniformValidation(string cursor)
    {
        var exception = Assert.Throws<ValidationException>(() => CheckInListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_WithOversizedCursor_ShouldThrowUniformValidation()
    {
        var cursor = new string('A', 257);

        var exception = Assert.Throws<ValidationException>(() => CheckInListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_WithInvalidUtf8Bytes_ShouldThrowUniformValidation()
    {
        var cursor = ToBase64Url(new byte[] { 0xFF, 0xFE, 0xFD });

        var exception = Assert.Throws<ValidationException>(() => CheckInListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_WithZOffsetDesignator_ShouldAcceptAsUtc()
    {
        var cursor = ToBase64Url(
            "1|2026-10-09T15:04:05.1234567Z|550e8400-e29b-41d4-a716-446655440000");

        var key = CheckInListCursor.Decode(cursor);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.RecordedAt);
        Assert.Equal(Guid.Parse(SampleGuid), key.CheckInId);
        Assert.Equal(TimeSpan.Zero, key.RecordedAt.Offset);
    }

    [Fact]
    public void Decode_WithSupportedVersion_ShouldAcceptExactRoundTripFormat()
    {
        var plaintext = string.Create(
            CultureInfo.InvariantCulture,
            $"1|{Sample:O}|{SampleGuid}");
        var cursor = ToBase64Url(plaintext);

        var key = CheckInListCursor.Decode(cursor);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.RecordedAt);
        Assert.Equal(Guid.Parse(SampleGuid), key.CheckInId);
    }
}
