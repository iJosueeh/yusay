using Yusay.Domain.Common;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.UnitTests.Tracking;

public class DimensionAnchorTests
{
    [Fact]
    public void Create_WithValidParameters_ShouldInstantiateAnchor()
    {
        var anchor = DimensionAnchor.Create(3, "Moderado");

        Assert.Equal(3, anchor.Value);
        Assert.Equal("Moderado", anchor.Label);
        Assert.Equal("3: \"Moderado\"", anchor.ToString());
    }

    [Fact]
    public void Create_WithPaddedLabel_ShouldTrimLabel()
    {
        var anchor = DimensionAnchor.Create(1, "  Muy bajo  ");
        Assert.Equal("Muy bajo", anchor.Label);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithNullOrWhitespaceLabel_ShouldThrowDomainException(string? invalidLabel)
    {
        var ex = Assert.Throws<DomainException>(() => DimensionAnchor.Create(1, invalidLabel!));
        Assert.Contains("no puede estar vacía", ex.Message);
    }
}
