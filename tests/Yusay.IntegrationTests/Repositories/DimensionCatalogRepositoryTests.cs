using Dapper;
using Yusay.Domain.Tracking.Enums;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Repositories;

/// <summary>
/// Pruebas de integración del repositorio de catálogo (B4 de RF-021): visibilidad por
/// operación (publicable, histórica y completa), rehidratación de las entidades de
/// dominio, orden determinista (code, version, value), anclas anidadas en la instantánea
/// única y ausencia de versión visible. Los datos se siembran con INSERT directo por
/// prueba y las aserciones se filtran por los IDs creados, porque la base de la colección
/// es compartida con otras clases de pruebas.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class DimensionCatalogRepositoryTests
{
    private readonly PostgreSqlFixture _fixture;
    private readonly DimensionCatalogRepository _repository;

    public DimensionCatalogRepositoryTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _repository = new DimensionCatalogRepository(fixture.ConnectionFactory);
    }

    [Fact]
    public async Task ListPublishedAsync_ShouldReturnOnlyTheActiveVersionAndHideDraftOnlyDimensions()
    {
        var mixed = await SeedDimensionAsync("mixed");
        var retiredV1 = await SeedVersionAsync(mixed, 1, "RETIRED");
        var activeV2 = await SeedVersionAsync(mixed, 2, "ACTIVE");
        await SeedVersionAsync(mixed, 3, "DRAFT");

        var draftOnly = await SeedDimensionAsync("draftonly");
        await SeedVersionAsync(draftOnly, 1, "DRAFT");

        var retiredOnly = await SeedDimensionAsync("retiredonly");
        await SeedVersionAsync(retiredOnly, 1, "RETIRED");

        var entries = await _repository.ListPublishedAsync();
        var mine = entries
            .Where(entry => new[] { mixed, draftOnly, retiredOnly }.Contains(entry.Dimension.DimensionId))
            .ToArray();

        var visible = Assert.Single(mine);
        Assert.Equal(mixed, visible.Dimension.DimensionId);

        var version = Assert.Single(visible.Versions);
        Assert.Equal(activeV2, version.DimensionVersionId);
        Assert.Equal(DimensionVersionStatus.Active, version.Status);
        Assert.Equal(2, version.Version);
        Assert.NotEqual(retiredV1, version.DimensionVersionId);
    }

    [Fact]
    public async Task GetWithHistoryAsync_ShouldIncludeActiveAndRetiredVersionsButNeverDraft()
    {
        var dimensionId = await SeedDimensionAsync("history");
        var retiredV1 = await SeedVersionAsync(
            dimensionId, 1, "RETIRED",
            definition: "Definición congelada de la versión retirada.",
            anchors: [(5, "Ancla máxima"), (1, "Ancla mínima")]);
        var activeV2 = await SeedVersionAsync(dimensionId, 2, "ACTIVE");
        await SeedVersionAsync(dimensionId, 3, "DRAFT", definition: "Borrador que no debe filtrarse.");

        var entry = await _repository.GetWithHistoryAsync(dimensionId);

        Assert.NotNull(entry);
        Assert.Equal(dimensionId, entry.Dimension.DimensionId);
        Assert.Equal("Dimensión de prueba", entry.Dimension.Name);
        Assert.Equal(2, entry.Versions.Count);

        var first = entry.Versions[0];
        Assert.Equal(retiredV1, first.DimensionVersionId);
        Assert.Equal(DimensionVersionStatus.Retired, first.Status);
        Assert.Equal("Definición congelada de la versión retirada.", first.Definition);
        Assert.Equal((1, 5, 1), (first.Scale.MinValue, first.Scale.MaxValue, first.Scale.Step));
        Assert.Equal(new[] { 1, 5 }, first.Anchors.Select(anchor => anchor.Value));
        Assert.Equal(new[] { "Ancla mínima", "Ancla máxima" }, first.Anchors.Select(anchor => anchor.Label));

        Assert.Equal(activeV2, entry.Versions[1].DimensionVersionId);
        Assert.Equal(DimensionVersionStatus.Active, entry.Versions[1].Status);
    }

    [Fact]
    public async Task GetWithHistoryAsync_ShouldReturnNullForADimensionWithOnlyDraftVersions()
    {
        var dimensionId = await SeedDimensionAsync("draftonly");
        await SeedVersionAsync(dimensionId, 1, "DRAFT");

        Assert.Null(await _repository.GetWithHistoryAsync(dimensionId));
        Assert.DoesNotContain(
            await _repository.ListPublishedAsync(),
            entry => entry.Dimension.DimensionId == dimensionId);
    }

    [Fact]
    public async Task GetWithHistoryAsync_ShouldReturnNullForAnUnknownDimension()
    {
        Assert.Null(await _repository.GetWithHistoryAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ListCompleteAsync_ShouldIncludeDraftsAndADimensionWithoutVersions()
    {
        var mixed = await SeedDimensionAsync("adm_mixed");
        await SeedVersionAsync(mixed, 1, "RETIRED");
        await SeedVersionAsync(mixed, 2, "ACTIVE");
        await SeedVersionAsync(mixed, 3, "DRAFT");

        var draftOnly = await SeedDimensionAsync("adm_draftonly");
        await SeedVersionAsync(draftOnly, 1, "DRAFT");

        var pending = await SeedDimensionAsync("adm_pending");

        var entries = await _repository.ListCompleteAsync();
        var mine = entries
            .Where(entry => new[] { mixed, draftOnly, pending }.Contains(entry.Dimension.DimensionId))
            .ToArray();

        Assert.Equal(3, mine.Length);

        var mixedEntry = Assert.Single(mine, entry => entry.Dimension.DimensionId == mixed);
        Assert.Equal(
            new[] { DimensionVersionStatus.Retired, DimensionVersionStatus.Active, DimensionVersionStatus.Draft },
            mixedEntry.Versions.Select(version => version.Status));

        var draftEntry = Assert.Single(mine, entry => entry.Dimension.DimensionId == draftOnly);
        Assert.Equal(DimensionVersionStatus.Draft, Assert.Single(draftEntry.Versions).Status);

        var pendingEntry = Assert.Single(mine, entry => entry.Dimension.DimensionId == pending);
        Assert.Empty(pendingEntry.Versions);
    }

    [Fact]
    public async Task ListPublishedAndGetWithHistoryAsync_ShouldOrderByCodeVersionAndAnchorValue()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var zulu = await SeedDimensionAsync($"b4r_zulu_{suffix}");
        var alpha = await SeedDimensionAsync($"b4r_alpha_{suffix}");
        var mid = await SeedDimensionAsync($"b4r_mid_{suffix}");

        // Inserciones deliberadamente desordenadas respecto al orden de respuesta: el
        // orden lo determina SQL, no el orden de siembra. Los RETIRED se siembran antes
        // que el ACTIVE porque uxp_dimension_version_single_active admite como máximo
        // una versión ACTIVE por dimensión en cualquier instante.
        var v2 = await SeedVersionAsync(zulu, 2, "RETIRED");
        var v1 = await SeedVersionAsync(zulu, 1, "RETIRED");
        var v3 = await SeedVersionAsync(
            zulu, 3, "ACTIVE", anchors: [(5, "Ancla 5"), (1, "Ancla 1"), (3, "Ancla 3")]);
        await SeedVersionAsync(alpha, 1, "ACTIVE");
        await SeedVersionAsync(mid, 1, "ACTIVE");

        var entries = await _repository.ListPublishedAsync();
        var mine = entries
            .Where(entry => new[] { zulu, alpha, mid }.Contains(entry.Dimension.DimensionId))
            .ToArray();

        Assert.Equal(new[] { alpha, mid, zulu }, mine.Select(entry => entry.Dimension.DimensionId));

        var zuluEntry = await _repository.GetWithHistoryAsync(zulu);
        Assert.NotNull(zuluEntry);
        Assert.Equal(
            new[] { v1, v2, v3 },
            zuluEntry.Versions.Select(version => version.DimensionVersionId));
        Assert.Equal(
            new[] { 1, 3, 5 },
            Assert.Single(zuluEntry.Versions, v => v.DimensionVersionId == v3)
                .Anchors.Select(anchor => anchor.Value));
    }

    private async Task<Guid> SeedDimensionAsync(string label)
    {
        var dimensionId = Guid.NewGuid();
        var code = $"b4r_{label}_{Guid.NewGuid():N}";

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, @Code, 'Dimensión de prueba', 'Descripción de prueba.');
            """, new { DimensionId = dimensionId, Code = code }));

        return dimensionId;
    }

    private async Task<Guid> SeedVersionAsync(
        Guid dimensionId,
        int version,
        string status,
        string definition = "Definición de prueba.",
        int minValue = 1,
        int maxValue = 5,
        int step = 1,
        (int Value, string Label)[]? anchors = null)
    {
        var versionId = Guid.NewGuid();

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        // V015 prohíbe insertar anclas en versiones ACTIVE/RETIRED: se siembra como DRAFT,
        // se registran las anclas y solo después se transiciona al estado final
        // (DRAFT -> ACTIVE y, si procede, ACTIVE -> RETIRED, según el autómata de V015).
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, @Version, @Definition, @MinValue, @MaxValue, @Step, 'DRAFT');
            """, new
            {
                VersionId = versionId,
                DimensionId = dimensionId,
                Version = version,
                Definition = definition,
                MinValue = minValue,
                MaxValue = maxValue,
                Step = step
            }));

        foreach (var (value, label) in anchors ?? [])
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label)
                VALUES (@VersionId, @Value, @Label);
                """, new { VersionId = versionId, Value = value, Label = label }));
        }

        if (status is "ACTIVE" or "RETIRED")
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @VersionId;",
                new { VersionId = versionId }));
        }

        if (status == "RETIRED")
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = @VersionId;",
                new { VersionId = versionId }));
        }

        return versionId;
    }
}
