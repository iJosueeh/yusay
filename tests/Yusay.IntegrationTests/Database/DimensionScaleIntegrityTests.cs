using System.Diagnostics;
using Dapper;
using Npgsql;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Database;

/// <summary>
/// Pruebas de integración de V017 — Integridad de escala de DimensionVersion (B2 de RF-021):
/// G1 (anclas dentro de la escala incluso en DRAFT, rango y alineación al step — D-B2-1),
/// G2 (revalidación al activar, sin mínimo de anclas — D-B2-2), G3 (definition inmutable en
/// ACTIVE/RETIRED con semántica OLD.status — D-B2-3) y G4 (revalidación al cambiar la escala
/// en DRAFT). Incluye reemplazo atómico, concurrencia real entre dos conexiones con
/// sincronización explícita y tiempos de espera controlados, y regresiones de V015.
/// Los errores se afirman por SQLSTATE (23514/23503/23505) sin traducción HTTP (D-B2-4).
/// </summary>
[Collection("DatabaseCollection")]
public sealed class DimensionScaleIntegrityTests
{
    private static readonly TimeSpan BlockingWait = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan StatementWait = TimeSpan.FromSeconds(30);

    private readonly PostgreSqlFixture _fixture;

    public DimensionScaleIntegrityTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    // ------------------------------------------------------------------------------------------
    // Semillas y utilidades
    // ------------------------------------------------------------------------------------------

    private async Task<(Guid DimensionId, Guid VersionId)> SeedVersionAsync(
        string status = "DRAFT",
        int minValue = 1,
        int maxValue = 5,
        int step = 1)
    {
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, 'dim_' || replace(@DimensionId::text, '-', ''), 'Dimensión de prueba', 'Escala de prueba.');
            """, new { DimensionId = dimensionId }));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, 1, 'Versión de prueba.', @MinValue, @MaxValue, @Step, @Status);
            """, new
            {
                VersionId = versionId,
                DimensionId = dimensionId,
                MinValue = minValue,
                MaxValue = maxValue,
                Step = step,
                Status = status
            }));

        return (dimensionId, versionId);
    }

    private async Task<Guid> AddVersionAsync(
        Guid dimensionId,
        int version,
        string status,
        int minValue = 1,
        int maxValue = 5,
        int step = 1)
    {
        var versionId = Guid.NewGuid();
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, @Version, 'Versión de prueba.', @MinValue, @MaxValue, @Step, @Status);
            """, new
            {
                VersionId = versionId,
                DimensionId = dimensionId,
                Version = version,
                MinValue = minValue,
                MaxValue = maxValue,
                Step = step,
                Status = status
            }));
        return versionId;
    }

    private static CommandDefinition AnchorInsert(Guid versionId, int value) =>
        new(
            "INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label) VALUES (@VersionId, @Value, @Label)",
            new { VersionId = versionId, Value = value, Label = $"Ancla {value}" });

    private async Task ExecuteAsync(string sql, object? parameters = null)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters));
    }

    private async Task<long> CountAsync(string sql, object? parameters = null)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition(sql, parameters));
    }

    private async Task<string> GetStatusAsync(Guid versionId) =>
        await CountStringAsync("SELECT status FROM yusay.dimension_version WHERE dimension_version_id = @Id",
            new { Id = versionId });

    private async Task<string> CountStringAsync(string sql, object? parameters)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return (await connection.ExecuteScalarAsync<string>(new CommandDefinition(sql, parameters)))!;
    }

    private async Task<int[]> GetScaleAsync(Guid versionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return (await connection.ExecuteScalarAsync<int[]>(
            new CommandDefinition(
                "SELECT ARRAY[min_value, max_value, step] FROM yusay.dimension_version WHERE dimension_version_id = @Id",
                new { Id = versionId })))!;
    }

    private async Task<long> CountAnchorsAsync(Guid versionId) =>
        await CountAsync("SELECT count(*) FROM yusay.dimension_anchor WHERE dimension_version_id = @Id",
            new { Id = versionId });

    private static async Task<PostgresException> AssertSqlStateAsync(string expectedState, Func<Task> operation)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(operation);
        Assert.Equal(expectedState, exception.SqlState);
        return exception;
    }

    /// <summary>
    /// Espera explícita y acotada a que la sesión indicada quede bloqueada (wait_event_type
    /// 'Lock', que cubre locks de fila, transacción y virtualxid). Devuelve false si el plazo
    /// termina sin observar bloqueo.
    /// </summary>
    private async Task<bool> WaitForBlockingAsync(int pid, TimeSpan? timeout = null)
    {
        var limit = timeout ?? BlockingWait;
        var stopwatch = Stopwatch.StartNew();

        await using var monitor = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        while (stopwatch.Elapsed < limit)
        {
            var waiting = await monitor.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT count(*)::int FROM pg_stat_activity WHERE pid = @Pid AND wait_event_type = 'Lock'",
                new { Pid = pid }));
            if (waiting > 0)
            {
                return true;
            }

            await Task.Delay(100);
        }

        return false;
    }

    // ------------------------------------------------------------------------------------------
    // G1 — anclas dentro de la escala, incluso en DRAFT (D-B2-1)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task InsertAnchor_AtMinValue_InDraft_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync();

        await ExecuteAsync(AnchorInsert(versionId, 1).CommandText, AnchorInsert(versionId, 1).Parameters);

        Assert.Equal(1, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task InsertAnchor_AtMaxValue_InDraft_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync();

        await ExecuteAsync(AnchorInsert(versionId, 5).CommandText, AnchorInsert(versionId, 5).Parameters);

        Assert.Equal(1, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task InsertAnchor_AtStepAlignedMidpoint_InDraft_ShouldSucceed()
    {
        // Escala 1..5 con step 2: los valores alcanzables son 1, 3 y 5
        var (_, versionId) = await SeedVersionAsync(step: 2);

        await ExecuteAsync(AnchorInsert(versionId, 3).CommandText, AnchorInsert(versionId, 3).Parameters);

        Assert.Equal(1, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task InsertAnchor_BelowMinValue_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync();

        var exception = await AssertSqlStateAsync("23514",
            () => ExecuteAsync(AnchorInsert(versionId, 0).CommandText, AnchorInsert(versionId, 0).Parameters));

        Assert.Contains("fuera de la escala", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task InsertAnchor_AboveMaxValue_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync();

        var exception = await AssertSqlStateAsync("23514",
            () => ExecuteAsync(AnchorInsert(versionId, 6).CommandText, AnchorInsert(versionId, 6).Parameters));

        Assert.Contains("fuera de la escala", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task InsertAnchor_InRangeButNotStepAligned_ShouldRaiseCheckViolation()
    {
        // Escala 1..5 con step 2: value 2 está en rango pero no es alcanzable ((2-1) % 2 = 1)
        var (_, versionId) = await SeedVersionAsync(step: 2);

        await AssertSqlStateAsync("23514",
            () => ExecuteAsync(AnchorInsert(versionId, 2).CommandText, AnchorInsert(versionId, 2).Parameters));

        Assert.Equal(0, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task UpdateAnchor_ValueOutOfRange_InDraft_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync();
        await ExecuteAsync(AnchorInsert(versionId, 4).CommandText, AnchorInsert(versionId, 4).Parameters);

        await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_anchor SET value = 9 WHERE dimension_version_id = @Id AND value = 4",
            new { Id = versionId }));

        Assert.Equal(1, await CountAnchorsAsync(versionId));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.dimension_anchor WHERE dimension_version_id = @Id AND value = 4",
            new { Id = versionId }));
    }

    [Fact]
    public async Task UpdateAnchor_LabelOnly_InDraft_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync();
        await ExecuteAsync(AnchorInsert(versionId, 4).CommandText, AnchorInsert(versionId, 4).Parameters);

        await ExecuteAsync(
            "UPDATE yusay.dimension_anchor SET label = 'Etiqueta revisada' WHERE dimension_version_id = @Id AND value = 4",
            new { Id = versionId });

        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.dimension_anchor WHERE dimension_version_id = @Id AND label = 'Etiqueta revisada'",
            new { Id = versionId }));
    }

    [Fact]
    public async Task InsertAnchor_OnActiveVersion_ShouldRaiseCheckViolation_FromV015Immutability()
    {
        var (_, versionId) = await SeedVersionAsync(status: "ACTIVE");

        var exception = await AssertSqlStateAsync("23514",
            () => ExecuteAsync(AnchorInsert(versionId, 3).CommandText, AnchorInsert(versionId, 3).Parameters));

        // V015 responde primero (orden alfabético: immutability < scale): regresión intacta
        Assert.Contains("no se permite alterar anclajes", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task InsertAnchor_WithNonexistentVersion_ShouldRaiseForeignKeyViolation23503()
    {
        // G1 cede el paso a la FK: la referencia a una DimensionVersion inexistente debe
        // seguir produciendo 23503 (no se sustituye por 23514)
        await AssertSqlStateAsync("23503",
            () => ExecuteAsync(AnchorInsert(Guid.NewGuid(), 3).CommandText, AnchorInsert(Guid.NewGuid(), 3).Parameters));
    }

    [Fact]
    public async Task UpdateAnchor_ReassociatingToAnotherVersion_ShouldRaiseCheckViolation_FromV015()
    {
        var (_, firstVersionId) = await SeedVersionAsync();
        var (_, secondVersionId) = await SeedVersionAsync();
        await ExecuteAsync(AnchorInsert(firstVersionId, 3).CommandText, AnchorInsert(firstVersionId, 3).Parameters);

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_anchor SET dimension_version_id = @Target WHERE dimension_version_id = @Source AND value = 3",
            new { Target = secondVersionId, Source = firstVersionId }));

        Assert.Contains("reasociar", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, await CountAnchorsAsync(secondVersionId));
    }

    // ------------------------------------------------------------------------------------------
    // G2 — revalidación al activar, sin mínimo general (D-B2-2)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Activate_DraftWithValidAnchors_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync();
        for (var value = 1; value <= 5; value++)
        {
            await ExecuteAsync(AnchorInsert(versionId, value).CommandText, AnchorInsert(versionId, value).Parameters);
        }

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal("ACTIVE", await GetStatusAsync(versionId));
        Assert.Equal(5, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task Activate_DraftWithZeroAnchors_ShouldSucceed()
    {
        // D-B2-2: cero anclas es válido; la cardinalidad 0..N permanece intacta
        var (_, versionId) = await SeedVersionAsync();

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal("ACTIVE", await GetStatusAsync(versionId));
        Assert.Equal(0, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task Activate_DraftWithOutOfScaleAnchor_ShouldRaiseCheckViolation()
    {
        // La ancla inválida se siembra con triggers deshabilitados (session_replication_role
        // requiere superusuario, como la conexión de la colección) para simular datos previos
        // a V017: G2 es la defensa en profundidad que debe detectarlos al activar.
        var (_, versionId) = await SeedVersionAsync();

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync("SET session_replication_role = replica");
            await connection.ExecuteAsync(AnchorInsert(versionId, 99).CommandText, AnchorInsert(versionId, 99).Parameters);
            await connection.ExecuteAsync("SET session_replication_role = origin");
        }

        Assert.Equal(1, await CountAnchorsAsync(versionId));

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("No se puede activar", exception.Message, StringComparison.Ordinal);
        Assert.Equal("DRAFT", await GetStatusAsync(versionId));
    }

    [Fact]
    public async Task Activate_InvalidStatusTransitions_ShouldRaiseCheckViolation_FromV015()
    {
        // DRAFT -> RETIRED
        var (_, draftId) = await SeedVersionAsync(status: "DRAFT");
        var draftException = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = @Id",
            new { Id = draftId }));
        Assert.Contains("Transición de estado inválida", draftException.Message, StringComparison.Ordinal);
        Assert.Equal("DRAFT", await GetStatusAsync(draftId));

        // RETIRED -> ACTIVE
        var (_, retiredId) = await SeedVersionAsync(status: "RETIRED");
        var retiredException = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = retiredId }));
        Assert.Contains("Transición de estado inválida", retiredException.Message, StringComparison.Ordinal);
        Assert.Equal("RETIRED", await GetStatusAsync(retiredId));

        // ACTIVE -> DRAFT
        var (_, activeId) = await SeedVersionAsync(status: "ACTIVE");
        var activeException = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'DRAFT' WHERE dimension_version_id = @Id",
            new { Id = activeId }));
        Assert.Contains("Transición de estado inválida", activeException.Message, StringComparison.Ordinal);
        Assert.Equal("ACTIVE", await GetStatusAsync(activeId));
    }

    [Fact]
    public async Task Activate_CombinedScaleChangeWithAnchorsStillValid_ShouldSucceed()
    {
        // Anclas 1, 3, 5 con step 2; la sentencia combinada amplía a 1..7 (step 2) y activa
        var (_, versionId) = await SeedVersionAsync(step: 2);
        foreach (var value in new[] { 1, 3, 5 })
        {
            await ExecuteAsync(AnchorInsert(versionId, value).CommandText, AnchorInsert(versionId, value).Parameters);
        }

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET min_value = 1, max_value = 7, step = 2, status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal("ACTIVE", await GetStatusAsync(versionId));
        Assert.Equal(new[] { 1, 7, 2 }, await GetScaleAsync(versionId));
    }

    [Fact]
    public async Task Activate_CombinedScaleChangeInvalidatingAnchors_ShouldRaiseCheckViolation()
    {
        // Anclas {1, 5} en escala 1..5; la sentencia combinada encoge a 2..4 y activa
        var (_, versionId) = await SeedVersionAsync();
        foreach (var value in new[] { 1, 5 })
        {
            await ExecuteAsync(AnchorInsert(versionId, value).CommandText, AnchorInsert(versionId, value).Parameters);
        }

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET min_value = 2, max_value = 4, status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("No se puede activar", exception.Message, StringComparison.Ordinal);
        Assert.Equal("DRAFT", await GetStatusAsync(versionId));
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
    }

    // ------------------------------------------------------------------------------------------
    // G3 — definition inmutable en ACTIVE/RETIRED (D-B2-3)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdateDefinition_InDraft_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync(status: "DRAFT");

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET definition = 'Definición revisada en DRAFT.' WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal("Definición revisada en DRAFT.", await CountStringAsync(
            "SELECT definition FROM yusay.dimension_version WHERE dimension_version_id = @Id",
            new { Id = versionId }));
    }

    [Fact]
    public async Task UpdateDefinition_InActiveVersion_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync(status: "ACTIVE");

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET definition = 'Edición ilegal.' WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("definition", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ACTIVE", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateDefinition_InRetiredVersion_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync(status: "RETIRED");

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET definition = 'Edición ilegal.' WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("definition", exception.Message, StringComparison.Ordinal);
        Assert.Contains("RETIRED", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateDefinitionAndRetire_InSameStatementFromActive_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync(status: "ACTIVE");

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET definition = 'Edición ilegal.', status = 'RETIRED' WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("definition", exception.Message, StringComparison.Ordinal);
        Assert.Equal("ACTIVE", await GetStatusAsync(versionId));
    }

    [Fact]
    public async Task UpdateStatus_ActiveToRetired_WithoutDefinitionChange_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync(status: "ACTIVE");

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal("RETIRED", await GetStatusAsync(versionId));
    }

    [Fact]
    public async Task UpdateDefinitionAndActivate_InSameStatementFromDraft_ShouldSucceed_ThenFreeze()
    {
        // D-B2-3: la edición de definition acompañando a la activación desde DRAFT se permite;
        // después de activada, definition queda inmutable.
        var (_, versionId) = await SeedVersionAsync(status: "DRAFT");

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET definition = 'Definición congelada al activar.', status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal("ACTIVE", await GetStatusAsync(versionId));
        Assert.Equal("Definición congelada al activar.", await CountStringAsync(
            "SELECT definition FROM yusay.dimension_version WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET definition = 'Segunda edición ilegal.' WHERE dimension_version_id = @Id",
            new { Id = versionId }));
    }

    // ------------------------------------------------------------------------------------------
    // G4 — revalidación de anclas al cambiar la escala en DRAFT
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ChangeScale_InDraftWithoutAnchors_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync();

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET min_value = 0, max_value = 10, step = 5 WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal(new[] { 0, 10, 5 }, await GetScaleAsync(versionId));
    }

    [Fact]
    public async Task ChangeScale_InDraftWideningRangeKeepingAnchorsValid_ShouldSucceed()
    {
        var (_, versionId) = await SeedVersionAsync();
        foreach (var value in new[] { 1, 5 })
        {
            await ExecuteAsync(AnchorInsert(versionId, value).CommandText, AnchorInsert(versionId, value).Parameters);
        }

        await ExecuteAsync(
            "UPDATE yusay.dimension_version SET max_value = 8 WHERE dimension_version_id = @Id",
            new { Id = versionId });

        Assert.Equal(new[] { 1, 8, 1 }, await GetScaleAsync(versionId));
        Assert.Equal(2, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task ChangeScale_InDraftLoweringMaxBelowAnchor_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync();
        await ExecuteAsync(AnchorInsert(versionId, 5).CommandText, AnchorInsert(versionId, 5).Parameters);

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET max_value = 4 WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("dejaría", exception.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
    }

    [Fact]
    public async Task ChangeScale_InDraftRaisingMinAboveAnchor_ShouldRaiseCheckViolation()
    {
        var (_, versionId) = await SeedVersionAsync();
        await ExecuteAsync(AnchorInsert(versionId, 1).CommandText, AnchorInsert(versionId, 1).Parameters);

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET min_value = 2 WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("dejaría", exception.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
    }

    [Fact]
    public async Task ChangeScale_InDraftChangingStepInvalidatingAnchors_ShouldRaiseCheckViolation()
    {
        // Anclas 1..5 (step 1); cambiar a step 2 mantiene el CHECK de V004 pero deja fuera
        // a las anclas 2 y 4 ((2-1) % 2 = 1)
        var (_, versionId) = await SeedVersionAsync();
        for (var value = 1; value <= 5; value++)
        {
            await ExecuteAsync(AnchorInsert(versionId, value).CommandText, AnchorInsert(versionId, value).Parameters);
        }

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET step = 2 WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        Assert.Contains("dejaría", exception.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
    }

    [Fact]
    public async Task ChangeScale_InActiveVersion_ShouldRaiseCheckViolation_FromV015Immutability()
    {
        var (_, versionId) = await SeedVersionAsync(status: "ACTIVE");

        var exception = await AssertSqlStateAsync("23514", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET max_value = 9 WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        // V015 responde primero (orden alfabético: immutability < validate): regresión intacta
        Assert.Contains("escala ni identidad", exception.Message, StringComparison.Ordinal);
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
    }

    [Fact]
    public async Task ChangeScale_InDraftWithZeroStep_ShouldRaise22012_FromV004EndpointCheck()
    {
        var (_, versionId) = await SeedVersionAsync();

        // G4 cede la palabra a los CHECK de V004 y no ejecuta su propio módulo con step = 0.
        // El SQLSTATE observado es 22012 (division_by_zero): ck_dimension_version_scale_endpoint
        // ((max - min) % step = 0) se evalúa antes que ck_dimension_version_step_positive
        // (orden alfabético: "scale_endpoint" < "step_positive"), de modo que el módulo por
        // cero de V004 ocurre antes de que step_positive pueda responder 23514. Es
        // comportamiento preexistente de V004 — idéntico con o sin V017 — que no se puede
        // corregir aquí sin alterar V001–V016. Riesgo para B4–B6: D-B2-4 deberá considerar
        // también 22012 al traducir errores de escala a respuestas HTTP.
        var exception = await AssertSqlStateAsync("22012", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET step = 0 WHERE dimension_version_id = @Id",
            new { Id = versionId }));

        // El statement completo se revirtió: la escala original sigue intacta
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
    }

    // ------------------------------------------------------------------------------------------
    // Reemplazo atómico y único activo (V010 / MP-PHYS-004)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AtomicReplacement_InsideSingleTransaction_ShouldLeaveExactlyOneActive()
    {
        var dimensionId = Guid.NewGuid();
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.dimension (dimension_id, code, name, description)
                VALUES (@DimensionId, 'dim_' || replace(@DimensionId::text, '-', ''), 'Dimensión de prueba', 'Escala de prueba.');
                """, new { DimensionId = dimensionId }));
        }

        var activeVersionId = await AddVersionAsync(dimensionId, version: 1, status: "ACTIVE");
        var draftVersionId = await AddVersionAsync(dimensionId, version: 2, status: "DRAFT");
        await ExecuteAsync(AnchorInsert(draftVersionId, 3).CommandText, AnchorInsert(draftVersionId, 3).Parameters);

        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await using var transaction = await connection.BeginTransactionAsync();
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = @Id",
                new { Id = activeVersionId },
                transaction));
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @Id",
                new { Id = draftVersionId },
                transaction));
            await transaction.CommitAsync();
        }

        Assert.Equal("RETIRED", await GetStatusAsync(activeVersionId));
        Assert.Equal("ACTIVE", await GetStatusAsync(draftVersionId));
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.dimension_version WHERE dimension_id = @Id AND status = 'ACTIVE'",
            new { Id = dimensionId }));
    }

    [Fact]
    public async Task Activate_SecondVersionWithoutRetiringFirst_ShouldRaiseUniqueViolation23505()
    {
        var dimensionId = Guid.NewGuid();
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.dimension (dimension_id, code, name, description)
                VALUES (@DimensionId, 'dim_' || replace(@DimensionId::text, '-', ''), 'Dimensión de prueba', 'Escala de prueba.');
                """, new { DimensionId = dimensionId }));
        }

        var activeVersionId = await AddVersionAsync(dimensionId, version: 1, status: "ACTIVE");
        var draftVersionId = await AddVersionAsync(dimensionId, version: 2, status: "DRAFT");

        var exception = await AssertSqlStateAsync("23505", () => ExecuteAsync(
            "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @Id",
            new { Id = draftVersionId }));

        Assert.Contains("uxp_dimension_version_single_active", exception.Message, StringComparison.Ordinal);
        Assert.Equal("DRAFT", await GetStatusAsync(draftVersionId));
        Assert.Equal("ACTIVE", await GetStatusAsync(activeVersionId));
    }

    // ------------------------------------------------------------------------------------------
    // Concurrencia real entre dos conexiones (sincronización explícita + estado final)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ConcurrentAnchorInsert_BlocksInvalidScaleChange_WhichFailsAfterCommit()
    {
        var (_, versionId) = await SeedVersionAsync();

        // Conexión A: INSERT de ancla en transacción abierta (el FOR SHARE se retiene hasta commit)
        await using var connectionA = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transactionA = await connectionA.BeginTransactionAsync();
        await connectionA.ExecuteAsync(new CommandDefinition(
            AnchorInsert(versionId, 3).CommandText,
            AnchorInsert(versionId, 3).Parameters,
            transactionA));

        // Conexión B: cambio de escala que excluiría la ancla (max 5 -> 2); debe bloquear
        await using var connectionB = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var pidB = await connectionB.QuerySingleAsync<int>("SELECT pg_backend_pid()");
        var taskB = Task.Run(async () =>
        {
            try
            {
                await connectionB.ExecuteAsync(new CommandDefinition(
                    "UPDATE yusay.dimension_version SET max_value = 2 WHERE dimension_version_id = @Id",
                    new { Id = versionId }));
                return (PostgresException?)null;
            }
            catch (PostgresException exception)
            {
                return exception;
            }
        });

        var observedBlocking = await WaitForBlockingAsync(pidB);
        await transactionA.CommitAsync();
        var errorB = await taskB.WaitAsync(StatementWait);

        Assert.True(observedBlocking,
            "El UPDATE de escala debía bloquearse contra el FOR SHARE de la inserción de anclas.");
        Assert.NotNull(errorB);
        Assert.Equal("23514", errorB!.SqlState);
        Assert.Contains("dejaría", errorB.Message, StringComparison.Ordinal);

        // Estado final: ancla conservada y escala intacta (la sentencia de B se revirtió)
        Assert.Equal(1, await CountAnchorsAsync(versionId));
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
    }

    [Fact]
    public async Task ConcurrentAnchorInsert_BlocksActivation_WhichSeesAnchorAndFailsAfterCommit()
    {
        var (_, versionId) = await SeedVersionAsync();

        // Conexión A: INSERT de ancla válida (value 5 en escala 1..5) en transacción abierta
        await using var connectionA = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transactionA = await connectionA.BeginTransactionAsync();
        await connectionA.ExecuteAsync(new CommandDefinition(
            AnchorInsert(versionId, 5).CommandText,
            AnchorInsert(versionId, 5).Parameters,
            transactionA));

        // Conexión B: sentencia combinada que encoge la escala a 1..3 y activa; debe bloquear
        // y, tras el commit de A, G2 debe ver la ancla recién commiteada y rechazar (23514)
        await using var connectionB = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var pidB = await connectionB.QuerySingleAsync<int>("SELECT pg_backend_pid()");
        var taskB = Task.Run(async () =>
        {
            try
            {
                await connectionB.ExecuteAsync(new CommandDefinition(
                    "UPDATE yusay.dimension_version SET max_value = 3, status = 'ACTIVE' WHERE dimension_version_id = @Id",
                    new { Id = versionId }));
                return (PostgresException?)null;
            }
            catch (PostgresException exception)
            {
                return exception;
            }
        });

        var observedBlocking = await WaitForBlockingAsync(pidB);
        await transactionA.CommitAsync();
        var errorB = await taskB.WaitAsync(StatementWait);

        Assert.True(observedBlocking,
            "La activación debía bloquearse contra el FOR SHARE de la inserción de anclas.");
        Assert.NotNull(errorB);
        Assert.Equal("23514", errorB!.SqlState);
        Assert.Contains("No se puede activar", errorB.Message, StringComparison.Ordinal);

        // Estado final: sigue en DRAFT con escala original y la ancla persiste
        Assert.Equal("DRAFT", await GetStatusAsync(versionId));
        Assert.Equal(new[] { 1, 5, 1 }, await GetScaleAsync(versionId));
        Assert.Equal(1, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task ConcurrentScaleChange_BlocksAnchorInsert_WhichValidatesAgainstLatestScale()
    {
        var (_, versionId) = await SeedVersionAsync();

        // Conexión A: cambio de escala (max 5 -> 3) en transacción abierta, sin commit
        await using var connectionA = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await using var transactionA = await connectionA.BeginTransactionAsync();
        await connectionA.ExecuteAsync(new CommandDefinition(
            "UPDATE yusay.dimension_version SET max_value = 3 WHERE dimension_version_id = @Id",
            new { Id = versionId },
            transactionA));

        // Conexión B: INSERT de ancla value 5 (válida en la escala antigua 1..5, inválida en la
        // nueva 1..3). Debe bloquear en el FOR SHARE de G1 y, tras el commit de A, la validación
        // debe resolverse contra la escala ya commiteada: 23514.
        await using var connectionB = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var pidB = await connectionB.QuerySingleAsync<int>("SELECT pg_backend_pid()");
        var taskB = Task.Run(async () =>
        {
            try
            {
                await connectionB.ExecuteAsync(AnchorInsert(versionId, 5).CommandText,
                    AnchorInsert(versionId, 5).Parameters);
                return (PostgresException?)null;
            }
            catch (PostgresException exception)
            {
                return exception;
            }
        });

        var observedBlocking = await WaitForBlockingAsync(pidB);
        await transactionA.CommitAsync();
        var errorB = await taskB.WaitAsync(StatementWait);

        Assert.True(observedBlocking,
            "La inserción de ancla debía bloquearse contra el FOR UPDATE del cambio de escala.");
        Assert.NotNull(errorB);
        Assert.Equal("23514", errorB!.SqlState);
        Assert.Contains("fuera de la escala", errorB.Message, StringComparison.Ordinal);

        // Estado final: escala nueva aplicada y ninguna ancla persistida
        Assert.Equal(new[] { 1, 3, 1 }, await GetScaleAsync(versionId));
        Assert.Equal(0, await CountAnchorsAsync(versionId));
    }

    [Fact]
    public async Task ConcurrentActivations_OfTwoDrafts_ShouldYieldExactlyOneActiveWithoutDeadlock()
    {
        var dimensionId = Guid.NewGuid();
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.dimension (dimension_id, code, name, description)
                VALUES (@DimensionId, 'dim_' || replace(@DimensionId::text, '-', ''), 'Dimensión de prueba', 'Escala de prueba.');
                """, new { DimensionId = dimensionId }));
        }

        var firstVersionId = await AddVersionAsync(dimensionId, version: 1, status: "DRAFT");
        var secondVersionId = await AddVersionAsync(dimensionId, version: 2, status: "DRAFT");

        // Sincronización explícita: ambas activaciones arrancan juntas tras la compuerta
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstTask = ActivateAsync(gate.Task, firstVersionId);
        var secondTask = ActivateAsync(gate.Task, secondVersionId);
        gate.SetResult();

        var results = await Task.WhenAll(firstTask, secondTask).WaitAsync(StatementWait);

        // Invariante: exactamente una activación gana; la otra falla con 23505 (índice parcial
        // uxp_dimension_version_single_active) y nunca con 40P01 (deadlock)
        Assert.Equal(1, results.Count(result => result is null));
        var loser = results.Single(result => result is not null);
        Assert.Equal("23505", loser!.SqlState);

        // Estado final de la base de datos: un único ACTIVE entre las dos versiones
        Assert.Equal(1, await CountAsync(
            "SELECT count(*) FROM yusay.dimension_version WHERE dimension_id = @Id AND status = 'ACTIVE'",
            new { Id = dimensionId }));
        var statuses = new[]
        {
            await GetStatusAsync(firstVersionId),
            await GetStatusAsync(secondVersionId)
        };
        Assert.Contains("ACTIVE", statuses);
        Assert.Contains("DRAFT", statuses);
    }

    private async Task<PostgresException?> ActivateAsync(Task gate, Guid versionId)
    {
        await gate;
        try
        {
            await ExecuteAsync(
                "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @Id",
                new { Id = versionId });
            return null;
        }
        catch (PostgresException exception)
        {
            return exception;
        }
    }
}
