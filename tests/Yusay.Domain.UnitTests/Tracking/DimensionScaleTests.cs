using Yusay.Domain.Common;
using Yusay.Domain.Tracking.ValueObjects;

namespace Yusay.Domain.UnitTests.Tracking;

public class DimensionScaleTests
{
    [Theory]
    [InlineData(1, 5, 1)]
    [InlineData(0, 10, 2)]
    [InlineData(-5, 5, 1)]
    [InlineData(-10, -2, 4)]
    [InlineData(1, 100, 33)]
    public void Create_WithValidParameters_ShouldInstantiateScale(int min, int max, int step)
    {
        var scale = DimensionScale.Create(min, max, step);

        Assert.Equal(min, scale.MinValue);
        Assert.Equal(max, scale.MaxValue);
        Assert.Equal(step, scale.Step);
    }

    [Theory]
    [InlineData(5, 5, 1)]
    [InlineData(5, 1, 1)]
    [InlineData(0, 0, 1)]
    [InlineData(10, -5, 1)]
    public void Create_WhenMinNotLessThanMax_ShouldThrowDomainException(int min, int max, int step)
    {
        var ex = Assert.Throws<DomainException>(() => DimensionScale.Create(min, max, step));
        Assert.Contains("estrictamente menor", ex.Message);
    }

    [Theory]
    [InlineData(1, 5, 0)]
    [InlineData(1, 5, -1)]
    [InlineData(0, 10, -5)]
    public void Create_WhenStepNotPositive_ShouldThrowDomainException(int min, int max, int step)
    {
        var ex = Assert.Throws<DomainException>(() => DimensionScale.Create(min, max, step));
        Assert.Contains("positivo mayor a cero", ex.Message);
    }

    [Theory]
    [InlineData(1, 6, 2)]   // 5 % 2 != 0
    [InlineData(0, 10, 3)]  // 10 % 3 != 0
    [InlineData(-5, 5, 3)]  // 10 % 3 != 0
    public void Create_WhenRangeNotDivisibleByStep_ShouldThrowDomainException(int min, int max, int step)
    {
        var ex = Assert.Throws<DomainException>(() => DimensionScale.Create(min, max, step));
        Assert.Contains("no es divisible exactamente", ex.Message);
    }

    [Fact]
    public void Create_WithExtremeIntValues_ShouldHandleRangeWithoutOverflow()
    {
        // Rango de int.MinValue a int.MaxValue es 4,294,967,295, divisible entre 1 y entre 3, 5, etc.
        var scale = DimensionScale.Create(int.MinValue, int.MaxValue, 1);
        Assert.Equal(int.MinValue, scale.MinValue);
        Assert.Equal(int.MaxValue, scale.MaxValue);
        Assert.Equal(1, scale.Step);

        // Prueba IsWithinScale con extremos
        Assert.True(scale.IsWithinScale(int.MinValue));
        Assert.True(scale.IsWithinScale(0));
        Assert.True(scale.IsWithinScale(int.MaxValue));
    }

    [Fact]
    public void IsWithinScale_WithValidOrdinalScale_ShouldValidateReachableValues()
    {
        var scale = DimensionScale.Create(1, 5, 1);

        for (int i = 1; i <= 5; i++)
        {
            Assert.True(scale.IsWithinScale(i));
        }

        Assert.False(scale.IsWithinScale(0));
        Assert.False(scale.IsWithinScale(6));
        Assert.False(scale.IsWithinScale(-10));
        Assert.False(scale.IsWithinScale(100));
        Assert.False(scale.IsWithinScale(int.MinValue));
        Assert.False(scale.IsWithinScale(int.MaxValue));
    }

    [Fact]
    public void IsWithinScale_WithSteppedScale_ShouldRejectMisalignedValues()
    {
        var scale = DimensionScale.Create(1, 5, 2); // 1, 3, 5

        Assert.True(scale.IsWithinScale(1));
        Assert.True(scale.IsWithinScale(3));
        Assert.True(scale.IsWithinScale(5));

        Assert.False(scale.IsWithinScale(2)); // Desalineado
        Assert.False(scale.IsWithinScale(4)); // Desalineado
        Assert.False(scale.IsWithinScale(0)); // Menor a min
        Assert.False(scale.IsWithinScale(6)); // Mayor a max
    }

    [Fact]
    public void IsWithinScale_WithDefaultStruct_ShouldReturnFalseWithoutDivisionByZero()
    {
        var defaultScale = default(DimensionScale);
        Assert.False(defaultScale.IsWithinScale(0));
        Assert.False(defaultScale.IsWithinScale(1));
    }

    [Fact]
    public void EnsureWithinScale_WhenValid_ShouldNotThrow()
    {
        var scale = DimensionScale.Create(1, 5, 1);
        var exception = Record.Exception(() => scale.EnsureWithinScale(3));
        Assert.Null(exception);
    }

    [Fact]
    public void EnsureWithinScale_WhenInvalid_ShouldThrowDomainException()
    {
        var scale = DimensionScale.Create(1, 5, 1);
        var ex = Assert.Throws<DomainException>(() => scale.EnsureWithinScale(6));
        Assert.Contains("no pertenece a la escala", ex.Message);
    }
}
