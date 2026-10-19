using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Yusay.Api.Contracts.Content;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Application.Identity.Tokens;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Http;

[Collection("DatabaseCollection")]
public sealed class PublishedContentHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret = "yusay-published-content-integration-test-secret-0123456789";
    private const string ValidPassword = "NormativePassword#2026";
    private const string MissingBearerMessage = "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public PublishedContentHttpTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepo = new UserAccountRepository(fixture.ConnectionFactory);
        _userCredentialRepo = new UserCredentialRepository(fixture.ConnectionFactory);
        _verificationTokenRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
        _auditEventRepo = new AuditEventRepository(fixture.ConnectionFactory);
        _passwordHasher = new Argon2idPasswordHasher(new Argon2Options
        {
            MemorySize = 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        });
        _jwtTokenService = new JwtTokenService(new JwtOptions
        {
            Secret = TestJwtSecret,
            AccessTokenLifetimeSeconds = 900
        });
    }

    public async Task InitializeAsync() => await _redis.InitializeAsync();
    public async Task DisposeAsync() => await _redis.DisposeAsync();

    // ------------------------------------------------------------------------------------------
    // 1. Autorización: 401 sin token, 200 con usuario regular
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Endpoints_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resGetTopics = await SendGetAsync(client, "/topics");
        await AssertProblemAsync(resGetTopics, HttpStatusCode.Unauthorized, MissingBearerMessage);

        var resGetResources = await SendGetAsync(client, "/resources");
        await AssertProblemAsync(resGetResources, HttpStatusCode.Unauthorized, MissingBearerMessage);

        var resGetResourceById = await SendGetAsync(client, $"/resources/{Guid.NewGuid()}");
        await AssertProblemAsync(resGetResourceById, HttpStatusCode.Unauthorized, MissingBearerMessage);
    }

    [Fact]
    public async Task Endpoints_WithRegularUser_ShouldReturn200()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resGetTopics = await SendGetAsync(client, "/topics", userToken);
        Assert.Equal(HttpStatusCode.OK, resGetTopics.StatusCode);

        var resGetResources = await SendGetAsync(client, "/resources", userToken);
        Assert.Equal(HttpStatusCode.OK, resGetResources.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // 2. Visibilidad: Solo PUBLISHED; DRAFT y RETIRED devuelven 404
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resources_ShouldExposeOnlyPublished_AndReturn404ForDraftOrRetired()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var topicCode = $"vis_t_{Guid.NewGuid():N}"[..20];
        var topicId = await CreateTopicDirectAsync(topicCode, "Vis Topic");

        // 1. Crear recurso DRAFT
        var draftRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Draft Article {{Guid.NewGuid():N}}","topicIds":["{{topicId}}"]}"""), adminToken);
        draftRes.EnsureSuccessStatusCode();
        var draftId = await ReadResourceIdAsync(draftRes);

        // 2. Crear recurso PUBLISHED
        var pubRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Published Article {{Guid.NewGuid():N}}","body":"Contenido editorial completo","topicIds":["{{topicId}}"]}"""), adminToken);
        pubRes.EnsureSuccessStatusCode();
        var pubId = await ReadResourceIdAsync(pubRes);
        var publishResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{pubId}/publish", Json("{}"), adminToken);
        publishResp.EnsureSuccessStatusCode();

        // 3. Crear y retirar recurso
        var retRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Retired Article {{Guid.NewGuid():N}}","body":"Contenido para retirar","topicIds":["{{topicId}}"]}"""), adminToken);
        retRes.EnsureSuccessStatusCode();
        var retId = await ReadResourceIdAsync(retRes);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{retId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{retId}/retire", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // Act & Assert: Listado público por topicId
        var listResp = await SendGetAsync(client, $"/resources?topicId={topicId}", userToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var listData = await listResp.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.NotNull(listData);
        Assert.Contains(listData.Items, r => r.ResourceId == pubId);
        Assert.DoesNotContain(listData.Items, r => r.ResourceId == draftId);
        Assert.DoesNotContain(listData.Items, r => r.ResourceId == retId);

        // Detalle de PUBLISHED -> 200 con contenido completo
        var detailPub = await SendGetAsync(client, $"/resources/{pubId}", userToken);
        Assert.Equal(HttpStatusCode.OK, detailPub.StatusCode);
        var detailData = await detailPub.Content.ReadFromJsonAsync<ResourceDetailsResponse>();
        Assert.NotNull(detailData);
        Assert.Equal(pubId, detailData.ResourceId);
        Assert.Equal("Contenido editorial completo", detailData.Body);
        Assert.Single(detailData.Topics);

        // Detalle de DRAFT -> 404
        var detailDraft = await SendGetAsync(client, $"/resources/{draftId}", userToken);
        await AssertProblemAsync(detailDraft, HttpStatusCode.NotFound, "El recurso solicitado no existe");

        // Detalle de RETIRED -> 404
        var detailRetired = await SendGetAsync(client, $"/resources/{retId}", userToken);
        await AssertProblemAsync(detailRetired, HttpStatusCode.NotFound, "El recurso solicitado no existe");

        // Detalle de ID inexistente -> 404
        var detailNonExistent = await SendGetAsync(client, $"/resources/{Guid.NewGuid()}", userToken);
        await AssertProblemAsync(detailNonExistent, HttpStatusCode.NotFound, "El recurso solicitado no existe");
    }

    // ------------------------------------------------------------------------------------------
    // 3. Transición PUBLISHED -> RETIRED: desaparece inmediatamente
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resource_WhenTransitionedToRetired_ShouldImmediatelyDisappearFromClientView()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var topicCode = $"trans_t_{Guid.NewGuid():N}"[..20];
        var topicId = await CreateTopicDirectAsync(topicCode, "Trans Topic");

        var createRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Live Article {{Guid.NewGuid():N}}","body":"Contenido editorial activo","topicIds":["{{topicId}}"]}"""), adminToken);
        createRes.EnsureSuccessStatusCode();
        var resourceId = await ReadResourceIdAsync(createRes);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // 1. Verificamos que es visible
        var initialDetail = await SendGetAsync(client, $"/resources/{resourceId}", userToken);
        Assert.Equal(HttpStatusCode.OK, initialDetail.StatusCode);

        // 2. Administrador retira el recurso
        var retireResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/retire", Json("{}"), adminToken);
        Assert.Equal(HttpStatusCode.OK, retireResp.StatusCode);

        // 3. Inmediatamente ya no aparece en listado ni detalle
        var afterList = await SendGetAsync(client, $"/resources?topicId={topicId}", userToken);
        var afterListData = await afterList.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.DoesNotContain(afterListData!.Items, r => r.ResourceId == resourceId);

        var afterDetail = await SendGetAsync(client, $"/resources/{resourceId}", userToken);
        await AssertProblemAsync(afterDetail, HttpStatusCode.NotFound, "El recurso solicitado no existe");
    }

    // ------------------------------------------------------------------------------------------
    // 4. Filtros y regla de único contexto temático
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resources_WithMultipleContexts_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var topicId = Guid.NewGuid();
        var dimensionId = Guid.NewGuid();
        var attemptId = Guid.NewGuid();

        var resTopicAndDim = await SendGetAsync(client, $"/resources?topicId={topicId}&dimensionId={dimensionId}", userToken);
        await AssertProblemAsync(resTopicAndDim, HttpStatusCode.BadRequest, "Solo se permite especificar un criterio de contexto");

        var resTopicAndAttempt = await SendGetAsync(client, $"/resources?topicId={topicId}&attemptId={attemptId}", userToken);
        await AssertProblemAsync(resTopicAndAttempt, HttpStatusCode.BadRequest, "Solo se permite especificar un criterio de contexto");

        var resDimAndAttempt = await SendGetAsync(client, $"/resources?dimensionId={dimensionId}&attemptId={attemptId}", userToken);
        await AssertProblemAsync(resDimAndAttempt, HttpStatusCode.BadRequest, "Solo se permite especificar un criterio de contexto");
    }

    [Fact]
    public async Task Resources_WithTypeFilter_ShouldFilterCorrectly_OrRejectInvalidType()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var topicCode = $"type_t_{Guid.NewGuid():N}"[..20];
        var topicId = await CreateTopicDirectAsync(topicCode, "Type Topic");

        // Publicar 1 ARTICLE
        var artRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Type Test Article","body":"Cuerpo de prueba","topicIds":["{{topicId}}"]}"""), adminToken);
        var artId = await ReadResourceIdAsync(artRes);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{artId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // Publicar 1 EXTERNAL_LINK
        var extRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"EXTERNAL_LINK","title":"Type Test Link","externalUrl":"https://example.com/guide","topicIds":["{{topicId}}"]}"""), adminToken);
        var extId = await ReadResourceIdAsync(extRes);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{extId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // Filtrar por ARTICLE
        var artList = await SendGetAsync(client, $"/resources?topicId={topicId}&type=ARTICLE", userToken);
        Assert.Equal(HttpStatusCode.OK, artList.StatusCode);
        var artData = await artList.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.Single(artData!.Items);
        Assert.Equal(artId, artData.Items[0].ResourceId);
        Assert.Equal("ARTICLE", artData.Items[0].Type);

        // Filtrar por EXTERNAL_LINK
        var extList = await SendGetAsync(client, $"/resources?topicId={topicId}&type=EXTERNAL_LINK", userToken);
        Assert.Equal(HttpStatusCode.OK, extList.StatusCode);
        var extData = await extList.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.Single(extData!.Items);
        Assert.Equal(extId, extData.Items[0].ResourceId);
        Assert.Equal("EXTERNAL_LINK", extData.Items[0].Type);

        // Tipo inválido -> 400
        var invalidTypeResp = await SendGetAsync(client, "/resources?type=PODCAST", userToken);
        await AssertProblemAsync(invalidTypeResp, HttpStatusCode.BadRequest, "no es válido");
    }

    // ------------------------------------------------------------------------------------------
    // 5. Recorridos temáticos: Dimension -> Topic -> Resource y deduplicación
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resources_ByDimensionId_ShouldResolveTopicsAndDeduplicateResources()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        // Crear dimensión y 2 temas asociados a esa dimensión
        var dimId = Guid.NewGuid();
        var dimCode = $"dim_{Guid.NewGuid():N}"[..20];
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO yusay.dimension (dimension_id, code, name, description) VALUES (@Id, @Code, 'Dim Title', 'Dim Desc')",
                new { Id = dimId, Code = dimCode });
        }

        var topic1Id = await CreateTopicDirectAsync($"dt1_{Guid.NewGuid():N}"[..20], "Dim Topic 1");
        var topic2Id = await CreateTopicDirectAsync($"dt2_{Guid.NewGuid():N}"[..20], "Dim Topic 2");

        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync("INSERT INTO yusay.dimension_topic (dimension_id, topic_id) VALUES (@DimId, @TId)",
                new[] { new { DimId = dimId, TId = topic1Id }, new { DimId = dimId, TId = topic2Id } });
        }

        // Crear 1 recurso vinculado a AMBOS temas de la misma dimensión
        var resBoth = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Deduplicated Resource","body":"Body","topicIds":["{{topic1Id}}","{{topic2Id}}"]}"""), adminToken);
        var sharedResId = await ReadResourceIdAsync(resBoth);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{sharedResId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // Act: Consultar por dimensionId
        var listResp = await SendGetAsync(client, $"/resources?dimensionId={dimId}", userToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        var listData = await listResp.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.NotNull(listData);

        // Assert: El recurso aparece exactamente UNA vez (sin duplicados M:N)
        var matches = listData.Items.Where(r => r.ResourceId == sharedResId).ToList();
        Assert.Single(matches);
        Assert.Equal(2, matches[0].TopicCount);
    }

    // ------------------------------------------------------------------------------------------
    // 6. Recorrido temático por attemptId y Aislamiento por Usuario
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resources_ByAttemptId_ShouldResolveInterpretationTopics_AndIsolateByUser()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (user1Id, user1Token) = await CreateSessionAsync();
        var (_, user2Token) = await CreateSessionAsync();

        // 1. Crear instrumento, versión e interpretación con tema asociado
        var (instId, verId, q1, opt1_1, _, q2, opt2_1, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "guidance");

        // Obtener el interpretation_id generado para score 0
        Guid interpretationId;
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            interpretationId = await conn.QuerySingleAsync<Guid>(
                "SELECT interpretation_id FROM yusay.interpretation WHERE instrument_version_id = @VerId AND lower_bound = 0",
                new { VerId = verId });
        }

        // Crear topic y vincular a la interpretación
        var topicId = await CreateTopicDirectAsync($"att_t_{Guid.NewGuid():N}"[..20], "Attempt Topic");
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO yusay.interpretation_topic (interpretation_id, topic_id) VALUES (@InterpId, @TId)",
                new { InterpId = interpretationId, TId = topicId });
        }

        // Crear recurso publicado vinculado a ese topic
        var res = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Guidance Recommended Resource","body":"Guidance body","topicIds":["{{topicId}}"]}"""), adminToken);
        var resourceId = await ReadResourceIdAsync(res);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // 2. User1 inicia el intento
        var startResp = await SendPostAsync(client, $"/instruments/{instId}/attempts", user1Token);
        startResp.EnsureSuccessStatusCode();
        var attemptId = (await startResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("attemptId").GetGuid();

        // 3. User1 intenta consultar con el intento aún IN_PROGRESS -> 404 (sin resultado oficial)
        var inProgressResp = await SendGetAsync(client, $"/resources?attemptId={attemptId}", user1Token);
        await AssertProblemAsync(inProgressResp, HttpStatusCode.NotFound, "El intento especificado no existe o no cuenta con un resultado oficial disponible.");

        // 4. User1 responde con score 0 y envía el intento (pasa a SUBMITTED)
        var saveResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers",
            Json($$"""{"answers":[{"questionId":"{{q1}}","optionId":"{{opt1_1}}"},{"questionId":"{{q2}}","optionId":"{{opt2_1}}"}]}"""), user1Token);
        saveResp.EnsureSuccessStatusCode();

        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", user1Token);
        submitResp.EnsureSuccessStatusCode();

        // 5. User1 consulta por attemptId -> 200 con el recurso correspondiente
        var submittedResp = await SendGetAsync(client, $"/resources?attemptId={attemptId}", user1Token);
        Assert.Equal(HttpStatusCode.OK, submittedResp.StatusCode);
        var submittedData = await submittedResp.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.NotNull(submittedData);
        Assert.Contains(submittedData.Items, r => r.ResourceId == resourceId);

        // 6. Aislamiento: User2 intenta consultar el attemptId de User1 -> 404
        var user2QueryResp = await SendGetAsync(client, $"/resources?attemptId={attemptId}", user2Token);
        await AssertProblemAsync(user2QueryResp, HttpStatusCode.NotFound, "El intento especificado no existe o no cuenta con un resultado oficial disponible.");

        // 7. Intento totalmente inexistente -> 404
        var nonExistentResp = await SendGetAsync(client, $"/resources?attemptId={Guid.NewGuid()}", user1Token);
        await AssertProblemAsync(nonExistentResp, HttpStatusCode.NotFound, "El intento especificado no existe o no cuenta con un resultado oficial disponible.");
    }

    // ------------------------------------------------------------------------------------------
    // 7. Paginación Keyset con orden (title ASC, resource_id ASC) y empates
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Resources_KeysetPagination_ShouldOrderCorrectlyAndHandleTies()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var topicId = await CreateTopicDirectAsync($"keyset_t_{Guid.NewGuid():N}"[..20], "Keyset Topic");

        // Crear 3 recursos: dos con el MISMO título (empate) y uno con título posterior
        var sharedTitle = $"A Tied Title {Guid.NewGuid():N}";
        var laterTitle = $"Z Later Title {Guid.NewGuid():N}";

        var res1 = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"{{sharedTitle}}","body":"Body 1","topicIds":["{{topicId}}"]}"""), adminToken);
        var id1 = await ReadResourceIdAsync(res1);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{id1}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        var res2 = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"{{sharedTitle}}","body":"Body 2","topicIds":["{{topicId}}"]}"""), adminToken);
        var id2 = await ReadResourceIdAsync(res2);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{id2}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        var res3 = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"{{laterTitle}}","body":"Body 3","topicIds":["{{topicId}}"]}"""), adminToken);
        var id3 = await ReadResourceIdAsync(res3);
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{id3}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // Determinar orden esperado de los dos empatados
        var expectedFirst = id1.CompareTo(id2) < 0 ? id1 : id2;
        var expectedSecond = id1.CompareTo(id2) < 0 ? id2 : id1;

        // Página 1: limit = 2
        var page1Resp = await SendGetAsync(client, $"/resources?topicId={topicId}&limit=2", userToken);
        Assert.Equal(HttpStatusCode.OK, page1Resp.StatusCode);
        var page1 = await page1Resp.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.NotNull(page1);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(expectedFirst, page1.Items[0].ResourceId);
        Assert.Equal(expectedSecond, page1.Items[1].ResourceId);
        Assert.NotNull(page1.NextCursor);

        // Página 2: con cursor de página 1
        var page2Resp = await SendGetAsync(client, $"/resources?topicId={topicId}&limit=2&cursor={page1.NextCursor}", userToken);
        Assert.Equal(HttpStatusCode.OK, page2Resp.StatusCode);
        var page2 = await page2Resp.Content.ReadFromJsonAsync<ListResourcesResponse>();
        Assert.NotNull(page2);
        Assert.Single(page2.Items);
        Assert.Equal(id3, page2.Items[0].ResourceId);
        Assert.Null(page2.NextCursor);

        // Cursor inválido -> 400
        var invalidCursorResp = await SendGetAsync(client, $"/resources?cursor=invalid!cursor==", userToken);
        await AssertProblemAsync(invalidCursorResp, HttpStatusCode.BadRequest, "El cursor proporcionado no es válido.");

        // Limit fuera de rango -> 400
        var limitZeroResp = await SendGetAsync(client, "/resources?limit=0", userToken);
        await AssertProblemAsync(limitZeroResp, HttpStatusCode.BadRequest, "El parámetro limit debe ser un entero entre 1 y 100.");

        var limit101Resp = await SendGetAsync(client, "/resources?limit=101", userToken);
        await AssertProblemAsync(limit101Resp, HttpStatusCode.BadRequest, "El parámetro limit debe ser un entero entre 1 y 100.");
    }

    // ------------------------------------------------------------------------------------------
    // 8. Catálogo público de temas: GET /topics
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Topics_ShouldReturnListOrderedDeterministically()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var t1 = await CreateTopicDirectAsync($"b_top_{Guid.NewGuid():N}"[..20], "B Topic");
        var t2 = await CreateTopicDirectAsync($"a_top_{Guid.NewGuid():N}"[..20], "A Topic");

        var topicsResp = await SendGetAsync(client, "/topics", userToken);
        Assert.Equal(HttpStatusCode.OK, topicsResp.StatusCode);
        var topicsData = await topicsResp.Content.ReadFromJsonAsync<ListTopicsResponse>();
        Assert.NotNull(topicsData);
        Assert.True(topicsData.Items.Count >= 2);

        // Verificar ordenación por code ASC
        var codes = topicsData.Items.Select(t => t.Code).ToList();
        var sortedCodes = codes.OrderBy(c => c, StringComparer.Ordinal).ToList();
        Assert.Equal(sortedCodes, codes);
    }

    // ------------------------------------------------------------------------------------------
    // 9. Ausencia de escrituras / Idempotencia de lectura
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ReadOperations_ShouldNotProduceAuditEventsOrMutateDatabase()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        int auditCountBefore = await GetAuditCountAsync();

        var getTopicsResp = await SendGetAsync(client, "/topics", userToken);
        getTopicsResp.EnsureSuccessStatusCode();

        var getResourcesResp = await SendGetAsync(client, "/resources", userToken);
        getResourcesResp.EnsureSuccessStatusCode();

        int auditCountAfter = await GetAuditCountAsync();
        Assert.Equal(auditCountBefore, auditCountAfter);
    }

    // ------------------------------------------------------------------------------------------
    // Auxiliares
    // ------------------------------------------------------------------------------------------

    private static async Task<Guid> ReadResourceIdAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("resourceId").GetGuid();
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
        });

    private static StringContent Json(string value) =>
        new(value, Encoding.UTF8, "application/json");

    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient client,
        string url,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendPostAsync(
        HttpClient client,
        string url,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = Json("{}");
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        HttpContent content,
        string? accessToken = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Content = content;
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync()
    {
        var email = $"pub-content-{Guid.NewGuid():N}@test.yusay.local";
        var registration = await new RegisterUserUseCase(
                _unitOfWork, _userAccountRepo, _userCredentialRepo, _verificationTokenRepo,
                _auditEventRepo, _passwordHasher, new SecureTokenService(),
                new NullEmailVerificationSender())
            .ExecuteAsync(new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));

        await new VerifyEmailUseCase(
                _unitOfWork, _userAccountRepo, _verificationTokenRepo, _auditEventRepo, new SecureTokenService())
            .ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));

        var session = await new SignInUseCase(
                _unitOfWork, _userAccountRepo, _userCredentialRepo, _auditEventRepo,
                _passwordHasher, _jwtTokenService)
            .ExecuteAsync(new SignInCommand(email, ValidPassword));

        return (registration.UserId, session.AccessToken);
    }

    private async Task<string> CreateSessionAsyncAndGrant()
    {
        var (userId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(userId);
        return accessToken;
    }

    private async Task GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
    }

    private async Task<Guid> CreateTopicDirectAsync(string code, string name)
    {
        var id = Guid.NewGuid();
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO yusay.topic (topic_id, code, name, description) VALUES (@Id, @Code, @Name, 'Topic test desc')",
            new { Id = id, Code = code, Name = name });
        return id;
    }

    private async Task<int> GetAuditCountAsync()
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*) FROM yusay.audit_event;");
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedSubstring)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        var detail = problem.Detail ?? problem.Title ?? string.Empty;
        Assert.Contains(expectedSubstring, detail, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(Guid InstrumentId, Guid VersionId, Guid Q1, Guid Opt1_1, Guid Opt1_2, Guid Q2, Guid Opt2_1, Guid Opt2_2)>
        CreateAndPublishInstrumentWithFullOptionsAsync(
            HttpClient client,
            string adminToken,
            string prefix)
    {
        var instResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments",
            JsonContent.Create(new
            {
                code = $"{prefix}_{Guid.NewGuid():N}"[..20],
                name = $"Instrument {prefix}",
                description = "General desc",
                purpose = "Psychometric purpose"
            }), adminToken);
        instResp.EnsureSuccessStatusCode();
        var instId = (await instResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instrumentId").GetGuid();

        var verResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instId}/versions",
            JsonContent.Create(new
            {
                version = 1,
                sourceDescription = "Fuente",
                population = "Población",
                administrationConditions = "Condiciones",
                licenseInformation = "CC-BY",
                limitations = "Sin limitaciones"
            }), adminToken);
        verResp.EnsureSuccessStatusCode();
        var verId = (await verResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instrumentVersionId").GetGuid();

        var payload = Json("""
        {
            "references": [
                { "referenceOrder": 1, "citation": "Ref citation", "url": "https://example.com" }
            ],
            "questions": [
                {
                    "position": 1,
                    "prompt": "Pregunta 1",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "Opción 1A", "contribution": 0 },
                        { "position": 2, "label": "Opción 1B", "contribution": 1 }
                    ]
                },
                {
                    "position": 2,
                    "prompt": "Pregunta 2",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "Opción 2A", "contribution": 0 },
                        { "position": 2, "label": "Opción 2B", "contribution": 1 }
                    ]
                }
            ],
            "interpretations": [
                { "label": "Normal", "description": "Rango normal", "lowerBound": 0, "upperBound": 1 },
                { "label": "Elevado", "description": "Rango elevated", "lowerBound": 2, "upperBound": 2 }
            ]
        }
        """);

        var draftResp = await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instId}/versions/{verId}/draft-definition", payload, adminToken);
        draftResp.EnsureSuccessStatusCode();

        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instId}/versions/{verId}/mark-ready", Json("{}"), adminToken)).EnsureSuccessStatusCode();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instId}/versions/{verId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var questions = (await conn.QueryAsync<dynamic>(
            "SELECT question_id, position FROM yusay.question WHERE instrument_version_id = @VerId ORDER BY position ASC",
            new { VerId = verId })).ToList();

        var actualQ1 = (Guid)questions[0].question_id;
        var actualQ2 = (Guid)questions[1].question_id;

        var q1Options = (await conn.QueryAsync<dynamic>(
            "SELECT option_id, position FROM yusay.answer_option WHERE question_id = @QId ORDER BY position ASC",
            new { QId = actualQ1 })).ToList();
        var actualOpt1_1 = (Guid)q1Options[0].option_id;
        var actualOpt1_2 = (Guid)q1Options[1].option_id;

        var q2Options = (await conn.QueryAsync<dynamic>(
            "SELECT option_id, position FROM yusay.answer_option WHERE question_id = @QId ORDER BY position ASC",
            new { QId = actualQ2 })).ToList();
        var actualOpt2_1 = (Guid)q2Options[0].option_id;
        var actualOpt2_2 = (Guid)q2Options[1].option_id;

        return (instId, verId, actualQ1, actualOpt1_1, actualOpt1_2, actualQ2, actualOpt2_1, actualOpt2_2);
    }
}
