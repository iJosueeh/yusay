using Yusay.Domain.Common;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.UnitTests.Tracking;

public class DimensionCodeTests
{
    [Theory]
    [InlineData("mood")]
    [InlineData("stress")]
    [InlineData("energy")]
    [InlineData("sleep")]
    [InlineData("mood_v1")]
    [InlineData("CUSTOM-CODE")]
    [InlineData("code123")]
    [InlineData("a")]
    [InlineData("Dimension_42-Alpha")]
    public void Create_WithValidCode_ShouldPreserveExactValue(string validCode)
    {
        var code = DimensionCode.Create(validCode);

        Assert.Equal(validCode, code.Value);
        Assert.Equal(validCode, code.ToString());
        Assert.Equal(validCode, (string)code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WithNullOrEmpty_ShouldThrowDomainException(string? invalidCode)
    {
        var ex = Assert.Throws<DomainException>(() => DimensionCode.Create(invalidCode!));
        Assert.Contains("no puede ser nulo ni vacío", ex.Message);
    }

    [Theory]
    [InlineData(" mood")]
    [InlineData("mood ")]
    [InlineData(" mood ")]
    [InlineData("mood code")]
    [InlineData("mood\tcode")]
    public void Create_WithLeadingTrailingOrInternalSpaces_ShouldThrowDomainExceptionWithoutTrimming(string codeWithSpaces)
    {
        var ex = Assert.Throws<DomainException>(() => DimensionCode.Create(codeWithSpaces));
        Assert.Contains("no es válido", ex.Message);
    }

    [Theory]
    [InlineData("mood@test")]
    [InlineData("mood.test")]
    [InlineData("mood#1")]
    [InlineData("mood$")]
    [InlineData("estrés")]
    [InlineData("ánimo")]
    [InlineData("code+plus")]
    public void Create_WithInvalidCharacters_ShouldThrowDomainException(string invalidCode)
    {
        var ex = Assert.Throws<DomainException>(() => DimensionCode.Create(invalidCode));
        Assert.Contains("no es válido", ex.Message);
    }

    [Fact]
    public void ExplicitCast_WithValidCode_ShouldInstantiateDimensionCode()
    {
        var code = (DimensionCode)"energy_level";
        Assert.Equal("energy_level", code.Value);
    }
}
