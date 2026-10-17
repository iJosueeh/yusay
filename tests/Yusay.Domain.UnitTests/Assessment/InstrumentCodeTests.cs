using Yusay.Domain.Assessment.ValueObjects;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Assessment;

public class InstrumentCodeTests
{
    [Theory]
    [InlineData("phq-9")]
    [InlineData("GAD7")]
    [InlineData("who5_wellbeing")]
    [InlineData("test-123_abc")]
    [InlineData("A")]
    public void Create_WithValidCode_ShouldInstantiate(string validCode)
    {
        var code = InstrumentCode.Create(validCode);
        Assert.Equal(validCode.Trim(), code.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyCode_ShouldThrowDomainException(string? invalidCode)
    {
        var ex = Assert.Throws<DomainException>(() => InstrumentCode.Create(invalidCode!));
        Assert.Contains("código del instrumento no puede estar vacío", ex.Message);
    }

    [Theory]
    [InlineData("phq 9")]
    [InlineData("gad.7")]
    [InlineData("code$")]
    [InlineData("code#1")]
    [InlineData("instrument/1")]
    public void Create_WithInvalidCharacters_ShouldThrowDomainException(string invalidCode)
    {
        var ex = Assert.Throws<DomainException>(() => InstrumentCode.Create(invalidCode));
        Assert.Contains("no es válido", ex.Message);
    }

    [Fact]
    public void Create_WithMoreThan64Chars_ShouldThrowDomainException()
    {
        var longCode = new string('a', 65);
        var ex = Assert.Throws<DomainException>(() => InstrumentCode.Create(longCode));
        Assert.Contains("64", ex.Message);
    }

    [Fact]
    public void Create_WithExactly64Chars_ShouldSucceed()
    {
        var exactCode = new string('a', 64);
        var code = InstrumentCode.Create(exactCode);
        Assert.Equal(exactCode, code.Value);
    }
}
