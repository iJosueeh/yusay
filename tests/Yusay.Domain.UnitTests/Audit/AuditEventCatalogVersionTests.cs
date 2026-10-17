using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Audit;

public class AuditEventCatalogVersionTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateCatalogVersionCreated_ShouldEmitAdministratorCatalogEventWithNullMetadata()
    {
        var versionId = Guid.NewGuid();
        var administratorId = Guid.NewGuid();

        var audit = AuditEvent.CreateCatalogVersionCreated(versionId, administratorId, OccurredAt);

        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(administratorId, audit.ActorUserId);
        Assert.Equal("DIMENSION_VERSION", audit.TargetType);
        Assert.Equal(versionId.ToString(), audit.TargetIdentifier);
        Assert.Equal(OccurredAt, audit.OccurredAt);
        Assert.Null(audit.Metadata);
        Assert.NotEqual(Guid.Empty, audit.AuditEventId);
    }

    [Fact]
    public void CreateCatalogVersionCreated_WithEmptyIdentifiers_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            AuditEvent.CreateCatalogVersionCreated(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() =>
            AuditEvent.CreateCatalogVersionCreated(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void CreateCatalogVersionUpdated_ShouldSerializeChangedFieldsAsJsonArray()
    {
        var versionId = Guid.NewGuid();
        var administratorId = Guid.NewGuid();

        var audit = AuditEvent.CreateCatalogVersionUpdated(
            versionId, administratorId, ["definition"], OccurredAt);

        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(administratorId, audit.ActorUserId);
        Assert.Equal("DIMENSION_VERSION", audit.TargetType);
        Assert.Equal(versionId.ToString(), audit.TargetIdentifier);
        Assert.Equal("""{"changed_fields":["definition"]}""", audit.Metadata);
    }

    [Fact]
    public void CreateCatalogVersionUpdated_WithSeveralFields_ShouldPreserveTheDeclaredOrder()
    {
        var audit = AuditEvent.CreateCatalogVersionUpdated(
            Guid.NewGuid(), Guid.NewGuid(),
            ["definition", "min_value", "max_value", "step"], OccurredAt);

        Assert.Equal(
            """{"changed_fields":["definition","min_value","max_value","step"]}""",
            audit.Metadata);
    }

    [Fact]
    public void CreateCatalogVersionUpdated_WithNullChangedFields_ShouldEmitNullMetadata()
    {
        var audit = AuditEvent.CreateCatalogVersionUpdated(
            Guid.NewGuid(), Guid.NewGuid(), null, OccurredAt);

        Assert.Null(audit.Metadata);
    }

    [Fact]
    public void CreateCatalogVersionUpdated_WithEmptyChangedFields_ShouldEmitNullMetadata()
    {
        var audit = AuditEvent.CreateCatalogVersionUpdated(
            Guid.NewGuid(), Guid.NewGuid(), [], OccurredAt);

        Assert.Null(audit.Metadata);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("version")]
    [InlineData("anchors")]
    public void CreateCatalogVersionUpdated_WithFieldOutsideTheClosedList_ShouldThrowDomainException(
        string field)
    {
        var exception = Assert.Throws<DomainException>(() =>
            AuditEvent.CreateCatalogVersionUpdated(Guid.NewGuid(), Guid.NewGuid(), [field]));

        Assert.Contains("lista cerrada", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateCatalogVersionUpdated_WithEmptyIdentifiers_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() =>
            AuditEvent.CreateCatalogVersionUpdated(Guid.Empty, Guid.NewGuid(), ["definition"]));
        Assert.Throws<DomainException>(() =>
            AuditEvent.CreateCatalogVersionUpdated(Guid.NewGuid(), Guid.Empty, ["definition"]));
    }
}
