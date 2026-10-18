using System.Globalization;
using System.Text;
using Yusay.Application.Assessment.Queries.ListAssessments;
using Yusay.Application.Common.Exceptions;

namespace Yusay.Domain.UnitTests.Assessment;

public class AssessmentListCursorTests
{
    private static readonly DateTimeOffset Sample =
        new DateTimeOffset(2026, 10, 18, 10, 0, 0, TimeSpan.Zero).AddTicks(1234567);

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
        var encoded = AssessmentListCursor.Encode(Sample, Guid.Parse(SampleGuid));

        var key = AssessmentListCursor.Decode(encoded);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.StartedAt);
        Assert.Equal(Guid.Parse(SampleGuid), key.AttemptId);
        Assert.Equal(TimeSpan.Zero, key.StartedAt.Offset);
    }

    [Fact]
    public void Encode_WithNonUtcOffset_ShouldNormalizeToUtc()
    {
        var encoded = AssessmentListCursor.Encode(Sample.ToOffset(TimeSpan.FromHours(2)), Guid.Parse(SampleGuid));

        var key = AssessmentListCursor.Decode(encoded);

        Assert.NotNull(key);
        Assert.Equal(Sample, key.StartedAt);
    }

    [Fact]
    public void Encode_ShouldProduceUnpaddedBase64Url()
    {
        var encoded = AssessmentListCursor.Encode(Sample, Guid.Parse(SampleGuid));

        Assert.NotEmpty(encoded);
        Assert.Matches("^[A-Za-z0-9_-]+$", encoded);
    }

    [Fact]
    public void Decode_WithoutCursor_ShouldReturnNull()
    {
        Assert.Null(AssessmentListCursor.Decode(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc+def/")]
    [InlineData("abcd")]
    [InlineData("YWJjZA==")]
    [InlineData("2|2026-10-18T10:00:00.1234567+00:00|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T10:00:00.1234567+00:00")]
    [InlineData("1|2026-10-18T10:00:00.1234567+00:00|550e8400-e29b-41d4-a716-446655440000|extra")]
    [InlineData("1|not-a-timestamp|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T10:00:00+00:00|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T10:00:00.1234567+02:00|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-18T10:00:00.1234567+00:00|not-a-guid")]
    public void Decode_WithMalformedPayload_ShouldThrowUniformValidation(string plaintext)
    {
        var cursor = plaintext.Length == 0 ? plaintext : ToBase64Url(plaintext);

        var exception = Assert.Throws<ValidationException>(() => AssessmentListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("ab+c")]
    [InlineData("==")]
    public void Decode_WithInvalidAlphabet_ShouldThrowUniformValidation(string cursor)
    {
        var exception = Assert.Throws<ValidationException>(() => AssessmentListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_WithOversizedCursor_ShouldThrowUniformValidation()
    {
        var oversized = new string('a', 257);

        var exception = Assert.Throws<ValidationException>(() => AssessmentListCursor.Decode(oversized));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_WithInvalidUtf8Sequence_ShouldThrowUniformValidation()
    {
        var invalidUtf8 = new byte[] { 0xC0, 0xAF };
        var cursor = ToBase64Url(invalidUtf8);

        var exception = Assert.Throws<ValidationException>(() => AssessmentListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }
}
