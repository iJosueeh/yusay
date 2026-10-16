using Yusay.Domain.Audit.Entities;
using Yusay.Domain.Common;

namespace Yusay.Domain.UnitTests.Audit;

// B5a de RF-021: fábricas de auditoría del catálogo administrativo de Dimensions dentro
// del catálogo cerrado de V011 — CATALOG_CREATED (fila 14, perfil N: metadata NULL) y
// CATALOG_UPDATED (fila 15, perfil C: única clave changed_fields con la lista cerrada
// name/description de DIMENSION). Ningún valor fuera del catálogo puede emitirse.
public class AuditEventCatalogTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateCatalogCreated_ShouldEmitAdministratorCatalogEventWithNullMetadata()
    {
        var dimensionId = Guid.NewGuid();
        var administratorId = Guid.NewGuid();

        var audit = AuditEvent.CreateCatalogCreated(dimensionId, administratorId, OccurredAt);

        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(administratorId, audit.ActorUserId);
        Assert.Equal("DIMENSION", audit.TargetType);
        Assert.Equal(dimensionId.ToString(), audit.TargetIdentifier);
        Assert.Equal(OccurredAt, audit.OccurredAt);
        Assert.Null(audit.Metadata);
        Assert.NotEqual(Guid.Empty, audit.AuditEventId);
    }

    [Fact]
    public void CreateCatalogCreated_WithEmptyIdentifiers_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => AuditEvent.CreateCatalogCreated(Guid.Empty, Guid.NewGuid()));
        Assert.Throws<DomainException>(() => AuditEvent.CreateCatalogCreated(Guid.NewGuid(), Guid.Empty));
    }

    [Fact]
    public void CreateCatalogUpdated_ShouldSerializeChangedFieldsAsJsonArray()
    {
        var dimensionId = Guid.NewGuid();
        var administratorId = Guid.NewGuid();

        var audit = AuditEvent.CreateCatalogUpdated(
            dimensionId, administratorId, ["name"], OccurredAt);

        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.ActorKind);
        Assert.Equal(administratorId, audit.ActorUserId);
        Assert.Equal("DIMENSION", audit.TargetType);
        Assert.Equal(dimensionId.ToString(), audit.TargetIdentifier);
        Assert.Equal("""{"changed_fields":["name"]}""", audit.Metadata);
    }

    [Fact]
    public void CreateCatalogUpdated_WithBothFields_ShouldPreserveTheDeclaredOrder()
    {
        var audit = AuditEvent.CreateCatalogUpdated(
            Guid.NewGuid(), Guid.NewGuid(), ["name", "description"], OccurredAt);

        Assert.Equal("""{"changed_fields":["name","description"]}""", audit.Metadata);
    }

    [Fact]
    public void CreateCatalogUpdated_WithEmptyOrNullChangedFields_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => AuditEvent.CreateCatalogUpdated(
            Guid.NewGuid(), Guid.NewGuid(), []));
        Assert.Throws<DomainException>(() => AuditEvent.CreateCatalogUpdated(
            Guid.NewGuid(), Guid.NewGuid(), null!));
    }

    [Fact]
    public void CreateCatalogUpdated_WithFieldOutsideTheClosedList_ShouldThrowDomainException()
    {
        // version y min_value pertenecen a DIMENSION_VERSION, no a DIMENSION (C-2/D06)
        var exception = Assert.Throws<DomainException>(() => AuditEvent.CreateCatalogUpdated(
            Guid.NewGuid(), Guid.NewGuid(), ["name", "version"]));

        Assert.Contains("lista cerrada", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateCatalogUpdated_WithEmptyIdentifiers_ShouldThrowDomainException()
    {
        Assert.Throws<DomainException>(() => AuditEvent.CreateCatalogUpdated(
            Guid.Empty, Guid.NewGuid(), ["name"]));
        Assert.Throws<DomainException>(() => AuditEvent.CreateCatalogUpdated(
            Guid.NewGuid(), Guid.Empty, ["name"]));
    }
}
