using Yusay.Domain.Common;
using Yusay.Domain.Content.Entities;
using Yusay.Domain.Content.Enums;
using Yusay.Domain.Content.ValueObjects;

namespace Yusay.Domain.UnitTests.Content;

public sealed class TopicDomainTests
{
    [Theory]
    [InlineData("anxiety")]
    [InlineData("SLEEP_HYGIENE")]
    [InlineData("depression-symptoms")]
    [InlineData("topic_123")]
    public void Create_WithValidCode_ShouldSucceed(string rawCode)
    {
        var code = TopicCode.Create(rawCode);
        var topic = Topic.Create(code, "Nombre de prueba", "Descripción opcional");

        Assert.Equal(rawCode.Trim(), topic.Code.Value);
        Assert.Equal("Nombre de prueba", topic.Name);
        Assert.Equal("Descripción opcional", topic.Description);
        Assert.NotEqual(Guid.Empty, topic.TopicId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid code with spaces")]
    [InlineData("topic#symbol")]
    [InlineData("topic.dot")]
    public void Create_WithInvalidCode_ShouldThrowDomainException(string rawCode)
    {
        Assert.Throws<DomainException>(() => TopicCode.Create(rawCode));
    }

    [Fact]
    public void Create_WithCodeExceeding64Chars_ShouldThrowDomainException()
    {
        var longCode = new string('a', 65);
        Assert.Throws<DomainException>(() => TopicCode.Create(longCode));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ShouldThrowDomainException(string name)
    {
        var code = TopicCode.Create("valid-code");
        Assert.Throws<DomainException>(() => Topic.Create(code, name, null));
    }

    [Fact]
    public void Create_WithNameExceeding128Chars_ShouldThrowDomainException()
    {
        var code = TopicCode.Create("valid-code");
        var longName = new string('n', 129);
        Assert.Throws<DomainException>(() => Topic.Create(code, longName, null));
    }

    [Fact]
    public void Update_ShouldUpdateNameAndDescription()
    {
        var code = TopicCode.Create("valid-code");
        var topic = Topic.Create(code, "Nombre Inicial", "Desc Inicial");

        topic.Update("Nuevo Nombre", "Nueva Desc");

        Assert.Equal("Nuevo Nombre", topic.Name);
        Assert.Equal("Nueva Desc", topic.Description);
    }
}
