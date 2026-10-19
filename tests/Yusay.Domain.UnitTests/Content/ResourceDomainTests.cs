using Yusay.Domain.Common;
using Yusay.Domain.Content.Entities;
using Yusay.Domain.Content.Enums;

namespace Yusay.Domain.UnitTests.Content;

public sealed class ResourceDomainTests
{
    [Fact]
    public void CreateDraft_WithValidArticle_ShouldSucceed()
    {
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Técnicas de relajación",
            "Resumen informativo",
            "Cuerpo del artículo",
            null,
            [Guid.NewGuid()]);

        Assert.Equal(ResourceType.Article, resource.Type);
        Assert.Equal(ResourceStatus.Draft, resource.Status);
        Assert.Equal("Técnicas de relajación", resource.Title);
        Assert.Equal("Resumen informativo", resource.Summary);
        Assert.Equal("Cuerpo del artículo", resource.Body);
        Assert.Null(resource.ExternalUrl);
        Assert.Single(resource.TopicIds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateDraft_WithEmptyTitle_ShouldThrowDomainException(string title)
    {
        Assert.Throws<DomainException>(() => Resource.CreateDraft(ResourceType.Article, title));
    }

    [Fact]
    public void CreateDraft_WithTitleExceeding256Chars_ShouldThrowDomainException()
    {
        var longTitle = new string('t', 257);
        Assert.Throws<DomainException>(() => Resource.CreateDraft(ResourceType.Article, longTitle));
    }

    [Fact]
    public void Publish_Article_WithoutBody_ShouldThrowDomainException()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título válido",
            "Resumen",
            body: null,
            externalUrl: null,
            topicIds: [topicId]);

        var ex = Assert.Throws<DomainException>(() => resource.Publish());
        Assert.Contains("requiere un cuerpo (body) no vacío", ex.Message);
    }

    [Fact]
    public void Publish_Article_WithExternalUrl_ShouldThrowDomainException()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título válido",
            "Resumen",
            body: "Cuerpo válido",
            externalUrl: "https://example.com/test",
            topicIds: [topicId]);

        var ex = Assert.Throws<DomainException>(() => resource.Publish());
        Assert.Contains("no debe tener external_url", ex.Message);
    }

    [Fact]
    public void Publish_ExternalLink_WithoutHttpsUrl_ShouldThrowDomainException()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.ExternalLink,
            "Título válido",
            "Resumen",
            body: null,
            externalUrl: "http://insecure.example.com/test",
            topicIds: [topicId]);

        var ex = Assert.Throws<DomainException>(() => resource.Publish());
        Assert.Contains("protocolo HTTPS", ex.Message);
    }

    [Fact]
    public void Publish_ExternalLink_WithBody_ShouldThrowDomainException()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.ExternalLink,
            "Título válido",
            "Resumen",
            body: "No debe tener cuerpo",
            externalUrl: "https://example.com/test",
            topicIds: [topicId]);

        var ex = Assert.Throws<DomainException>(() => resource.Publish());
        Assert.Contains("no debe tener cuerpo (body)", ex.Message);
    }

    [Fact]
    public void Publish_WithoutTopics_ShouldThrowDomainException()
    {
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título válido",
            "Resumen",
            body: "Cuerpo completo",
            externalUrl: null,
            topicIds: null);

        var ex = Assert.Throws<DomainException>(() => resource.Publish());
        Assert.Contains("al menos un tema asociado", ex.Message);
    }

    [Fact]
    public void Publish_ValidArticleWithTopics_ShouldTransitionToPublished()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título válido",
            "Resumen",
            body: "Cuerpo completo",
            externalUrl: null,
            topicIds: [topicId]);

        resource.Publish();

        Assert.Equal(ResourceStatus.Published, resource.Status);
    }

    [Fact]
    public void Retire_FromPublished_ShouldTransitionToRetired()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título válido",
            "Resumen",
            body: "Cuerpo completo",
            externalUrl: null,
            topicIds: [topicId]);

        resource.Publish();
        resource.Retire();

        Assert.Equal(ResourceStatus.Retired, resource.Status);
    }

    [Fact]
    public void Retire_FromDraft_ShouldThrowDomainException()
    {
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título borrador",
            body: "Cuerpo");

        var ex = Assert.Throws<DomainException>(() => resource.Retire());
        Assert.Contains("solo se puede retirar un recurso en estado PUBLISHED", ex.Message);
    }

    [Fact]
    public void Retire_IsTerminal_TransitionsFromRetiredShouldFail()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título",
            body: "Cuerpo",
            topicIds: [topicId]);

        resource.Publish();
        resource.Retire();

        Assert.Throws<DomainException>(() => resource.Publish());
        Assert.Throws<DomainException>(() => resource.UpdateDraft(ResourceType.Article, "Nuevo", null, "Cuerpo", null));
        Assert.Throws<DomainException>(() => resource.ApplyMinorCorrection("Nuevo", null, "Cuerpo", null, CorrectionKind.Spelling));
        Assert.Throws<DomainException>(() => resource.AddTopic(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => resource.RemoveTopic(topicId));
    }

    [Fact]
    public void RemoveTopic_WhenPublishedWithSingleTopic_ShouldThrowDomainException()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título",
            body: "Cuerpo",
            topicIds: [topicId]);

        resource.Publish();

        var ex = Assert.Throws<DomainException>(() => resource.RemoveTopic(topicId));
        Assert.Contains("No se puede desvincular el único tema de un recurso publicado", ex.Message);
    }

    [Fact]
    public void RemoveTopic_WhenPublishedWithMultipleTopics_ShouldSucceed()
    {
        var topic1 = Guid.NewGuid();
        var topic2 = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título",
            body: "Cuerpo",
            topicIds: [topic1, topic2]);

        resource.Publish();
        resource.RemoveTopic(topic1);

        Assert.Single(resource.TopicIds);
        Assert.Contains(topic2, resource.TopicIds);
    }

    [Fact]
    public void ApplyMinorCorrection_OnPublishedResource_ShouldUpdateContent()
    {
        var topicId = Guid.NewGuid();
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título original",
            body: "Cuerpo original",
            topicIds: [topicId]);

        resource.Publish();

        resource.ApplyMinorCorrection(
            "Título corregido",
            "Nuevo resumen",
            "Cuerpo corregido",
            null,
            CorrectionKind.Spelling);

        Assert.Equal("Título corregido", resource.Title);
        Assert.Equal("Nuevo resumen", resource.Summary);
        Assert.Equal("Cuerpo corregido", resource.Body);
    }

    [Fact]
    public void ApplyMinorCorrection_OnDraftResource_ShouldThrowDomainException()
    {
        var resource = Resource.CreateDraft(
            ResourceType.Article,
            "Título",
            body: "Cuerpo");

        var ex = Assert.Throws<DomainException>(() =>
            resource.ApplyMinorCorrection("Título", null, "Cuerpo", null, CorrectionKind.Spelling));
        Assert.Contains("Las correcciones editoriales menores solo aplican a recursos en estado PUBLISHED", ex.Message);
    }
}
