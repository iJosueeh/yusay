using System.Text;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Content.Queries.ListResources;

namespace Yusay.Domain.UnitTests.Content;

public class ResourceListCursorTests
{
    private const string SampleGuid = "550e8400-e29b-41d4-a716-446655440000";
    private const string InvalidMessage = "El cursor proporcionado no es válido.";

    private static string ToBase64Url(string plaintext) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(plaintext))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    [Theory]
    [InlineData("Alpha Resource")]
    [InlineData("Guía de Gestión del Estrés")]
    [InlineData("Title | With | Pipes")]
    [InlineData("12345")]
    public void Encode_ThenDecode_ShouldRoundTrip(string title)
    {
        var id = Guid.Parse(SampleGuid);
        var encoded = ResourceListCursor.Encode(title, id);

        var key = ResourceListCursor.Decode(encoded);

        Assert.NotNull(key);
        Assert.Equal(title, key.Title);
        Assert.Equal(id, key.ResourceId);
    }

    [Fact]
    public void Encode_ShouldProduceUnpaddedBase64Url()
    {
        var encoded = ResourceListCursor.Encode("Some Title", Guid.Parse(SampleGuid));

        Assert.NotEmpty(encoded);
        Assert.Matches("^[A-Za-z0-9_-]+$", encoded);
        Assert.DoesNotContain("=", encoded);
    }

    [Fact]
    public void Decode_WithoutCursor_ShouldReturnNull()
    {
        Assert.Null(ResourceListCursor.Decode(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc+def/")]
    [InlineData("YWJjZA==")]
    [InlineData("2|Some Title|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|Some Title")]
    [InlineData("1||550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|Some Title|")]
    [InlineData("|Some Title|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|Some Title|not-a-guid")]
    [InlineData("0|Some Title|550e8400-e29b-41d4-a716-446655440000")]
    public void Decode_WithMalformedPayload_ShouldThrowUniformValidation(string plaintext)
    {
        var cursor = plaintext.Length == 0 ? plaintext : ToBase64Url(plaintext);

        var exception = Assert.Throws<ValidationException>(() => ResourceListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("ab+c")]
    [InlineData("ab/c")]
    [InlineData("==")]
    public void Decode_WithInvalidAlphabet_ShouldThrowUniformValidation(string cursor)
    {
        var exception = Assert.Throws<ValidationException>(() => ResourceListCursor.Decode(cursor));

        Assert.Equal(InvalidMessage, exception.Message);
    }

    [Fact]
    public void Decode_ExceedingMaxLength_ShouldThrowUniformValidation()
    {
        var tooLong = new string('A', 513);
        var exception = Assert.Throws<ValidationException>(() => ResourceListCursor.Decode(tooLong));

        Assert.Equal(InvalidMessage, exception.Message);
    }
}
