using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Application.Identity.Tokens;
using Yusay.Domain.Audit.Entities;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Http;

[Collection("DatabaseCollection")]
public sealed class InstrumentAdminHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-instrument-admin-http-integration-test-secret-0123456789";

    private const string ValidPassword = "NormativePassword#2026";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private const string TokenRejectedMessage =
        "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private const string DuplicateCodeMessage =
        "Ya existe un instrumento con el código especificado.";

    private const string DuplicateVersionMessage =
        "Ya existe una versión con el número especificado para este instrumento.";

    private const string InstrumentNotFoundMessage =
        "El Instrumento solicitado no existe.";

    private const string VersionNotFoundMessage =
        "La versión solicitada no existe para este Instrumento.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public InstrumentAdminHttpTests(PostgreSqlFixture fixture)
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
    // Autorización (401, 403, 503)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Endpoints_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missingPostInst = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("auth")));
        await AssertProblemAsync(missingPostInst, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingPatchInst = await SendJsonAsync(client, HttpMethod.Patch, $"/admin/instruments/{Guid.NewGuid()}", Json("""{"name":"Nuevo"}"""));
        await AssertProblemAsync(missingPatchInst, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingPostVer = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions", CreateVersionBody(1));
        await AssertProblemAsync(missingPostVer, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingGetList = await SendGetAsync(client, "/admin/instruments");
        await AssertProblemAsync(missingGetList, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingGetInst = await SendGetAsync(client, $"/admin/instruments/{Guid.NewGuid()}");
        await AssertProblemAsync(missingGetInst, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingGetVer = await SendGetAsync(client, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}");
        await AssertProblemAsync(missingGetVer, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingPutDraft = await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/draft-definition", Json("""{"questions":[]}"""));
        await AssertProblemAsync(missingPutDraft, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingMarkReady = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/mark-ready", Json("{}"));
        await AssertProblemAsync(missingMarkReady, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingReturnDraft = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/return-to-draft", Json("{}"));
        await AssertProblemAsync(missingReturnDraft, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingPublish = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/publish", Json("{}"));
        await AssertProblemAsync(missingPublish, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingRetire = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/retire", Json("{}"));
        await AssertProblemAsync(missingRetire, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task Endpoints_WithNonAdministrator_ShouldReturn403()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var postInst = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("denied")), accessToken);
        await AssertProblemAsync(postInst, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var patchInst = await SendJsonAsync(client, HttpMethod.Patch, $"/admin/instruments/{Guid.NewGuid()}", Json("""{"name":"Nuevo"}"""), accessToken);
        await AssertProblemAsync(patchInst, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var postVer = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions", CreateVersionBody(1), accessToken);
        await AssertProblemAsync(postVer, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var getList = await SendGetAsync(client, "/admin/instruments", accessToken);
        await AssertProblemAsync(getList, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var getInst = await SendGetAsync(client, $"/admin/instruments/{Guid.NewGuid()}", accessToken);
        await AssertProblemAsync(getInst, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var getVer = await SendGetAsync(client, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}", accessToken);
        await AssertProblemAsync(getVer, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var putDraft = await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/draft-definition", Json("""{"questions":[]}"""), accessToken);
        await AssertProblemAsync(putDraft, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var markReady = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/mark-ready", Json("{}"), accessToken);
        await AssertProblemAsync(markReady, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var returnDraft = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/return-to-draft", Json("{}"), accessToken);
        await AssertProblemAsync(returnDraft, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var publish = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/publish", Json("{}"), accessToken);
        await AssertProblemAsync(publish, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var retire = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/retire", Json("{}"), accessToken);
        await AssertProblemAsync(retire, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");
    }

    [Fact]
    public async Task Endpoints_WhenAdminCheckIsUnavailable_ShouldReturn503FailClosed()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAdministratorAuthorizationRepository>();
            services.AddSingleton<IAdministratorAuthorizationRepository, UnreachableAdministratorRepository>();
        });
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var postInst = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("unreach")), accessToken);
        await AssertProblemAsync(postInst, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");
    }

    // ------------------------------------------------------------------------------------------
    // POST /admin/instruments
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateInstrument_WithValidData_ShouldReturn201AndPersistWithAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var code = NewCode("c1_post");
        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(code, "  PHQ-9 Depresión  ", "  Escala de 9 ítems.  ", "  Despistaje clínico.  "),
            accessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var instrumentId = root.GetProperty("instrumentId").GetGuid();
        Assert.NotEqual(Guid.Empty, instrumentId);
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal("PHQ-9 Depresión", root.GetProperty("name").GetString());
        Assert.Equal("Escala de 9 ítems.", root.GetProperty("description").GetString());
        Assert.Equal("Despistaje clínico.", root.GetProperty("purpose").GetString());

        var audit = Assert.Single(await QueryAuditAsync(instrumentId));
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("INSTRUMENT", audit.Target_type);
        Assert.Equal(instrumentId.ToString(), audit.Target_identifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task CreateInstrument_WithInvalidCodeFormat_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody("codigo con espacios!", "Nombre", "Desc", "Propósito"),
            accessToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Solicitud no válida", "no es válido");
    }

    [Fact]
    public async Task CreateInstrument_WithEmptyFields_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("empty"), "   ", "Desc", "Propósito"),
            accessToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "Solicitud no válida", "nombre del instrumento no puede estar vacío");
    }

    [Fact]
    public async Task CreateInstrument_WithDuplicateCode_ShouldReturn409Conflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var code = NewCode("dup");
        var first = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(code), accessToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(code), accessToken);
        await AssertProblemAsync(second, HttpStatusCode.Conflict, "Conflicto", DuplicateCodeMessage);
    }

    [Fact]
    public async Task CreateInstrument_ConcurrentIdenticalCode_ShouldProduceExactlyOne201AndOne409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var code = NewCode("race");
        var task1 = SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(code), accessToken);
        var task2 = SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(code), accessToken);

        var responses = await Task.WhenAll(task1, task2);
        var statuses = responses.Select(r => r.StatusCode).OrderBy(s => s).ToArray();

        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, statuses);
    }

    // ------------------------------------------------------------------------------------------
    // PATCH /admin/instruments/{id}
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PatchInstrument_WithChangedFields_ShouldReturn200AndAuditCatalogUpdated()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var created = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("patch"), "Nombre Original", "Desc Original", "Propósito Original"),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var instrumentId = await ReadGuidPropertyAsync(created, "instrumentId");

        var patchResponse = await SendJsonAsync(
            client,
            HttpMethod.Patch,
            $"/admin/instruments/{instrumentId}",
            Json("""{"name":"Nombre Modificado","purpose":"Nuevo Propósito"}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        using var doc = JsonDocument.Parse(await patchResponse.Content.ReadAsStringAsync());
        Assert.Equal("Nombre Modificado", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal("Desc Original", doc.RootElement.GetProperty("description").GetString());
        Assert.Equal("Nuevo Propósito", doc.RootElement.GetProperty("purpose").GetString());

        var updateAudits = await QueryAuditAsync(instrumentId, "CATALOG_UPDATED");
        var audit = Assert.Single(updateAudits);
        Assert.Equal("INSTRUMENT", audit.Target_type);
        var changedFields = ReadChangedFields(audit.Metadata);
        Assert.Contains("name", changedFields);
        Assert.Contains("purpose", changedFields);
        Assert.DoesNotContain("description", changedFields);
    }

    [Fact]
    public async Task PatchInstrument_WithNoEffectiveChanges_ShouldReturn200WithoutAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var created = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("no_chg"), "Nombre Fijo", "Desc Fija", "Propósito Fijo"),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var instrumentId = await ReadGuidPropertyAsync(created, "instrumentId");

        var patchResponse = await SendJsonAsync(
            client,
            HttpMethod.Patch,
            $"/admin/instruments/{instrumentId}",
            Json("""{"name":"Nombre Fijo","description":"Desc Fija"}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var updateAudits = await QueryAuditAsync(instrumentId, "CATALOG_UPDATED");
        Assert.Empty(updateAudits);
    }

    [Fact]
    public async Task PatchInstrument_WithNonExistentId_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var missingId = Guid.NewGuid();
        var response = await SendJsonAsync(
            client,
            HttpMethod.Patch,
            $"/admin/instruments/{missingId}",
            Json("""{"name":"Nuevo"}"""),
            accessToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Recurso no encontrado", InstrumentNotFoundMessage);
    }

    // ------------------------------------------------------------------------------------------
    // POST /admin/instruments/{id}/versions
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateVersion_WithExplicitVersion_ShouldReturn201WithScoringDefinitionAndAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var createdInst = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("ver1")),
            accessToken);
        var instrumentId = await ReadGuidPropertyAsync(createdInst, "instrumentId");

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(1, "Fuente bibliográfica", "Población general", "Individual", "CC-BY", "Limitaciones conocidas"),
            accessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var versionId = root.GetProperty("instrumentVersionId").GetGuid();
        Assert.NotEqual(Guid.Empty, versionId);
        Assert.Equal(instrumentId, root.GetProperty("instrumentId").GetGuid());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("DRAFT", root.GetProperty("status").GetString());
        Assert.Equal("Fuente bibliográfica", root.GetProperty("sourceDescription").GetString());
        Assert.Equal("Población general", root.GetProperty("population").GetString());
        Assert.Equal("Limitaciones conocidas", root.GetProperty("limitations").GetString());

        // Comprueba que scoring_definition (SUM) se insertó simultáneamente
        var scoringCount = await CountScoringDefinitionsAsync(versionId);
        Assert.Equal(1, scoringCount);

        var audit = Assert.Single(await QueryAuditAsync(versionId));
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("INSTRUMENT_VERSION", audit.Target_type);
        Assert.Equal(adminUserId, audit.Actor_user_id);
    }

    [Fact]
    public async Task CreateVersion_WithNullVersion_ShouldAutoIncrement()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var createdInst = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("auto_ver")),
            accessToken);
        var instrumentId = await ReadGuidPropertyAsync(createdInst, "instrumentId");

        var v1Response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(null, "Fuente 1"),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, v1Response.StatusCode);
        using (var doc1 = JsonDocument.Parse(await v1Response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(1, doc1.RootElement.GetProperty("version").GetInt32());
        }

        var v2Response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(null, "Fuente 2"),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, v2Response.StatusCode);
        using (var doc2 = JsonDocument.Parse(await v2Response.Content.ReadAsStringAsync()))
        {
            Assert.Equal(2, doc2.RootElement.GetProperty("version").GetInt32());
        }
    }

    [Fact]
    public async Task CreateVersion_WithDuplicateVersionNumber_ShouldReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var createdInst = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("dup_ver")),
            accessToken);
        var instrumentId = await ReadGuidPropertyAsync(createdInst, "instrumentId");

        var v1 = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(1),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, v1.StatusCode);

        var v1Dup = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(1),
            accessToken);
        await AssertProblemAsync(v1Dup, HttpStatusCode.Conflict, "Conflicto", DuplicateVersionMessage);
    }

    [Fact]
    public async Task CreateVersion_ConcurrentAutoIncrement_ShouldGenerateSequentialVersions()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var createdInst = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("conc_ver")),
            accessToken);
        var instrumentId = await ReadGuidPropertyAsync(createdInst, "instrumentId");

        var task1 = SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(null, "Fuente Conc 1"),
            accessToken);
        var task2 = SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(null, "Fuente Conc 2"),
            accessToken);

        var responses = await Task.WhenAll(task1, task2);
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Created, r.StatusCode));

        var ver1Doc = JsonDocument.Parse(await responses[0].Content.ReadAsStringAsync());
        var ver2Doc = JsonDocument.Parse(await responses[1].Content.ReadAsStringAsync());

        var v1 = ver1Doc.RootElement.GetProperty("version").GetInt32();
        var v2 = ver2Doc.RootElement.GetProperty("version").GetInt32();

        Assert.Equal(new[] { 1, 2 }, new[] { v1, v2 }.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task CreateVersion_WithNonExistentInstrument_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var missingId = Guid.NewGuid();
        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{missingId}/versions",
            CreateVersionBody(1),
            accessToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Recurso no encontrado", InstrumentNotFoundMessage);
    }

    // ------------------------------------------------------------------------------------------
    // Consultas administrativas (GET /admin/instruments, GET /admin/instruments/{id}, GET /admin/instruments/{id}/versions/{versionId})
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetQueries_ShouldReturnConsistentCatalogHierarchy()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var code = NewCode("queries");
        var instResp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(code, "Instrumento Queries", "Descripción", "Propósito"),
            accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            CreateVersionBody(1, "Fuente Q", "Población Q", null, null, "Limitaciones Q"),
            accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        // 1. List
        var listResp = await SendGetAsync(client, "/admin/instruments", accessToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        using var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var items = listDoc.RootElement.GetProperty("items").EnumerateArray().ToList();
        var foundInst = items.FirstOrDefault(i => i.GetProperty("instrumentId").GetGuid() == instrumentId);
        Assert.True(foundInst.ValueKind != JsonValueKind.Undefined);
        Assert.Equal(1, foundInst.GetProperty("totalVersions").GetInt32());

        // 2. Details
        var detailsResp = await SendGetAsync(client, $"/admin/instruments/{instrumentId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, detailsResp.StatusCode);
        using var detailsDoc = JsonDocument.Parse(await detailsResp.Content.ReadAsStringAsync());
        Assert.Equal(code, detailsDoc.RootElement.GetProperty("code").GetString());
        var versions = detailsDoc.RootElement.GetProperty("versions").EnumerateArray().ToList();
        var foundVer = Assert.Single(versions);
        Assert.Equal(versionId, foundVer.GetProperty("instrumentVersionId").GetGuid());
        Assert.Equal(1, foundVer.GetProperty("version").GetInt32());

        // 3. Version Details
        var verDetailsResp = await SendGetAsync(client, $"/admin/instruments/{instrumentId}/versions/{versionId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, verDetailsResp.StatusCode);
        using var verDetailsDoc = JsonDocument.Parse(await verDetailsResp.Content.ReadAsStringAsync());
        Assert.Equal(versionId, verDetailsDoc.RootElement.GetProperty("instrumentVersionId").GetGuid());
        Assert.Equal(instrumentId, verDetailsDoc.RootElement.GetProperty("instrumentId").GetGuid());
        Assert.Equal("DRAFT", verDetailsDoc.RootElement.GetProperty("status").GetString());
        Assert.Equal("Fuente Q", verDetailsDoc.RootElement.GetProperty("sourceDescription").GetString());

        // 4. Mismatched version on another instrument returns 404
        var otherInstResp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            CreateInstrumentBody(NewCode("other")),
            accessToken);
        var otherInstId = await ReadGuidPropertyAsync(otherInstResp, "instrumentId");

        var mismatchResp = await SendGetAsync(client, $"/admin/instruments/{otherInstId}/versions/{versionId}", accessToken);
        await AssertProblemAsync(mismatchResp, HttpStatusCode.NotFound, "Recurso no encontrado", VersionNotFoundMessage);
    }

    // ------------------------------------------------------------------------------------------
    // PUT /admin/instruments/{instrumentId}/versions/{versionId}/draft-definition (C2)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task ReplaceDraftDefinition_WithFullComposition_ShouldReturn200AndPersistAllRowsAndAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c2_full")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        var payload = Json("""
        {
            "references": [
                {
                    "referenceOrder": 1,
                    "citation": "Kroenke K, Spitzer RL, Williams JB. The PHQ-9. J Gen Intern Med. 2001.",
                    "url": "https://doi.org/10.1046/j.1525-1497.2001.016009606.x"
                }
            ],
            "questions": [
                {
                    "position": 1,
                    "prompt": "¿Poco interés o alegría por hacer las cosas?",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "Para nada", "contribution": 0 },
                        { "position": 2, "label": "Varios días", "contribution": 1 },
                        { "position": 3, "label": "Más de la mitad de los días", "contribution": 2 },
                        { "position": 4, "label": "Casi todos los días", "contribution": 3 }
                    ]
                },
                {
                    "position": 2,
                    "prompt": "¿Se ha sentido decaído/a, deprimido/a o sin esperanzas?",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "Para nada", "contribution": 0 },
                        { "position": 2, "label": "Varios días", "contribution": 1 },
                        { "position": 3, "label": "Más de la mitad de los días", "contribution": 2 },
                        { "position": 4, "label": "Casi todos los días", "contribution": 3 }
                    ]
                }
            ],
            "interpretations": [
                {
                    "label": "Mínima",
                    "description": "Depresión mínima.",
                    "limitations": null,
                    "lowerBound": 0,
                    "upperBound": 4
                },
                {
                    "label": "Leve",
                    "description": "Depresión leve.",
                    "limitations": "Sugerir seguimiento.",
                    "lowerBound": 5,
                    "upperBound": 9
                }
            ]
        }
        """);

        var response = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition", payload, accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(versionId, root.GetProperty("instrumentVersionId").GetGuid());
        Assert.Equal(instrumentId, root.GetProperty("instrumentId").GetGuid());
        Assert.Equal("DRAFT", root.GetProperty("status").GetString());
        Assert.Equal(1, root.GetProperty("references").GetArrayLength());
        Assert.Equal(2, root.GetProperty("questions").GetArrayLength());
        Assert.Equal(4, root.GetProperty("questions")[0].GetProperty("options").GetArrayLength());
        Assert.Equal(2, root.GetProperty("interpretations").GetArrayLength());

        // Verificar filas en PostgreSQL
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        var refCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.instrument_version_reference WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId });
        Assert.Equal(1, refCount);

        var qCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.question WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId });
        Assert.Equal(2, qCount);

        var optCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*) FROM yusay.answer_option ao
            JOIN yusay.question q ON q.question_id = ao.question_id
            WHERE q.instrument_version_id = @VersionId
            """,
            new { VersionId = versionId });
        Assert.Equal(8, optCount);

        var scCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.scoring_contribution WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId });
        Assert.Equal(8, scCount);

        var interpCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.interpretation WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId });
        Assert.Equal(2, interpCount);

        // Scoring definition (1:1 SUM) se mantiene exactamente una
        var sdCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.scoring_definition WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId });
        Assert.Equal(1, sdCount);

        // Evento de auditoría CATALOG_UPDATED sobre INSTRUMENT_VERSION
        var audits = await QueryAuditAsync(versionId, "CATALOG_UPDATED");
        var audit = Assert.Single(audits);
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("INSTRUMENT_VERSION", audit.Target_type);

        var changed = ReadChangedFields(audit.Metadata);
        Assert.Equal(new[] { "REFERENCES", "QUESTIONS", "ANSWER_OPTIONS", "SCORING", "INTERPRETATIONS" }, changed);
    }

    [Fact]
    public async Task ReplaceDraftDefinition_WithIdenticalPayload_ShouldBeNoOpPreservingUuidsAndNoAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c2_noop")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        var payload = Json("""
        {
            "references": [
                { "referenceOrder": 1, "citation": "Ref 1", "url": null }
            ],
            "questions": [
                {
                    "position": 1,
                    "prompt": "Pregunta 1",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "Opción 1", "contribution": 1 }
                    ]
                }
            ],
            "interpretations": [
                { "label": "Interp 1", "description": "Desc 1", "limitations": null, "lowerBound": 0, "upperBound": 1 }
            ]
        }
        """);

        // 1er PUT: Crea la composición
        var firstResp = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition", payload, accessToken);
        Assert.Equal(HttpStatusCode.OK, firstResp.StatusCode);

        using var firstDoc = JsonDocument.Parse(await firstResp.Content.ReadAsStringAsync());
        var firstQId = firstDoc.RootElement.GetProperty("questions")[0].GetProperty("questionId").GetGuid();
        var firstOptId = firstDoc.RootElement.GetProperty("questions")[0].GetProperty("options")[0].GetProperty("optionId").GetGuid();
        var firstInterpId = firstDoc.RootElement.GetProperty("interpretations")[0].GetProperty("interpretationId").GetGuid();

        var auditsBefore = await QueryAuditAsync(versionId, "CATALOG_UPDATED");
        Assert.Single(auditsBefore);

        // 2do PUT con payload idéntico: debe ser No-Op
        var identicalPayload = Json("""
        {
            "references": [
                { "referenceOrder": 1, "citation": "Ref 1", "url": null }
            ],
            "questions": [
                {
                    "position": 1,
                    "prompt": "Pregunta 1",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "Opción 1", "contribution": 1 }
                    ]
                }
            ],
            "interpretations": [
                { "label": "Interp 1", "description": "Desc 1", "limitations": null, "lowerBound": 0, "upperBound": 1 }
            ]
        }
        """);

        var secondResp = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition", identicalPayload, accessToken);
        Assert.Equal(HttpStatusCode.OK, secondResp.StatusCode);

        using var secondDoc = JsonDocument.Parse(await secondResp.Content.ReadAsStringAsync());
        var secondQId = secondDoc.RootElement.GetProperty("questions")[0].GetProperty("questionId").GetGuid();
        var secondOptId = secondDoc.RootElement.GetProperty("questions")[0].GetProperty("options")[0].GetProperty("optionId").GetGuid();
        var secondInterpId = secondDoc.RootElement.GetProperty("interpretations")[0].GetProperty("interpretationId").GetGuid();

        // UUIDs preservados
        Assert.Equal(firstQId, secondQId);
        Assert.Equal(firstOptId, secondOptId);
        Assert.Equal(firstInterpId, secondInterpId);

        // Sin nuevo evento de auditoría
        var auditsAfter = await QueryAuditAsync(versionId, "CATALOG_UPDATED");
        Assert.Single(auditsAfter);
    }

    [Fact]
    public async Task ReplaceDraftDefinition_WithChangedCategories_ShouldAuditOnlyModifiedCategories()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c2_diff")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        // 1er PUT: base
        await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""
            {
                "references": [
                    { "referenceOrder": 1, "citation": "Citation Original", "url": "https://example.com" }
                ],
                "questions": [
                    {
                        "position": 1,
                        "prompt": "Pregunta original",
                        "required": true,
                        "options": [
                            { "position": 1, "label": "Opción original", "contribution": 0 }
                        ]
                    }
                ],
                "interpretations": [
                    { "label": "Interp", "description": "Desc", "limitations": null, "lowerBound": 0, "upperBound": 1 }
                ]
            }
            """), accessToken);

        // 2do PUT: solo cambia preguntas y scoring (references e interpretations idénticas)
        var modifiedResp = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""
            {
                "references": [
                    { "referenceOrder": 1, "citation": "Citation Original", "url": "https://example.com" }
                ],
                "questions": [
                    {
                        "position": 1,
                        "prompt": "Pregunta MODIFICADA",
                        "required": true,
                        "options": [
                            { "position": 1, "label": "Opción original", "contribution": 5 }
                        ]
                    }
                ],
                "interpretations": [
                    { "label": "Interp", "description": "Desc", "limitations": null, "lowerBound": 0, "upperBound": 1 }
                ]
            }
            """), accessToken);

        Assert.Equal(HttpStatusCode.OK, modifiedResp.StatusCode);

        var audits = await QueryAuditAsync(versionId, "CATALOG_UPDATED");
        Assert.Equal(2, audits.Length);

        var secondAuditChanged = ReadChangedFields(audits[1].Metadata);
        Assert.Equal(new[] { "QUESTIONS", "SCORING" }, secondAuditChanged);
    }

    [Fact]
    public async Task ReplaceDraftDefinition_WithIncompleteDraftAndNullContributions_ShouldPersistWithoutScoringRows()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c2_incompl")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        // DRAFT incompleto: 0 referencias, 1 pregunta con opciones sin puntaje (null), 0 interpretaciones
        var response = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""
            {
                "references": [],
                "questions": [
                    {
                        "position": 1,
                        "prompt": "¿Pregunta cualitativa sin puntaje?",
                        "required": false,
                        "options": [
                            { "position": 1, "label": "Opción 1", "contribution": null },
                            { "position": 2, "label": "Opción 2", "contribution": null }
                        ]
                    }
                ],
                "interpretations": []
            }
            """), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var scCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.scoring_contribution WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId });
        Assert.Equal(0, scCount);

        var optCount = await connection.ExecuteScalarAsync<int>(
            """
            SELECT count(*) FROM yusay.answer_option ao
            JOIN yusay.question q ON q.question_id = ao.question_id
            WHERE q.instrument_version_id = @VersionId
            """,
            new { VersionId = versionId });
        Assert.Equal(2, optCount);
    }

    [Fact]
    public async Task ReplaceDraftDefinition_WhenVersionIsNotDraft_ShouldReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c2_notdraft")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        // Cambiar estado a READY en PostgreSQL
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE yusay.instrument_version SET status = 'READY' WHERE instrument_version_id = @VersionId",
                new { VersionId = versionId });
        }

        var response = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""{"questions":[]}"""), accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Conflicto", "DRAFT");
    }

    [Fact]
    public async Task ReplaceDraftDefinition_WhenNotFound_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var missingInstResp = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{Guid.NewGuid()}/versions/{Guid.NewGuid()}/draft-definition",
            Json("""{"questions":[]}"""), accessToken);
        await AssertProblemAsync(missingInstResp, HttpStatusCode.NotFound, "Recurso no encontrado", "no existe");
    }

    [Fact]
    public async Task ReplaceDraftDefinition_WithStructuralValidationErrors_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c2_val")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        // 1. Posición duplicada de preguntas
        var dupQResp = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""
            {
                "questions": [
                    { "position": 1, "prompt": "P1", "options": [] },
                    { "position": 1, "prompt": "P1 bis", "options": [] }
                ]
            }
            """), accessToken);
        await AssertProblemAsync(dupQResp, HttpStatusCode.BadRequest, "Solicitud no válida", "duplicada");

        // 2. Límites de interpretación invertidos
        var invBoundsResp = await SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""
            {
                "interpretations": [
                    { "label": "Inv", "description": "Desc", "lowerBound": 10, "upperBound": 5 }
                ]
            }
            """), accessToken);
        await AssertProblemAsync(invBoundsResp, HttpStatusCode.BadRequest, "Solicitud no válida", "límite inferior");
    }

    [Fact]
    public async Task ReplaceDraftDefinition_ConcurrentRequests_ShouldSerializeUnderForUpdate()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c2_conc")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        var task1 = SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""
            {
                "questions": [
                    { "position": 1, "prompt": "Concurrente A", "options": [] }
                ]
            }
            """), accessToken);

        var task2 = SendJsonAsync(
            client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            Json("""
            {
                "questions": [
                    { "position": 1, "prompt": "Concurrente B", "options": [] }
                ]
            }
            """), accessToken);

        var responses = await Task.WhenAll(task1, task2);
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var qCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.question WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId });
        Assert.Equal(1, qCount);
    }

    // ------------------------------------------------------------------------------------------
    // C3: Ciclo de Vida Editorial (mark-ready, return-to-draft, publish, retire)
    // ------------------------------------------------------------------------------------------

    private static HttpContent ValidPsychometricDraftPayload() => Json("""
    {
        "references": [
            { "referenceOrder": 1, "citation": "Kroenke K. 2001.", "url": "https://doi.org/10.1046/phq9" }
        ],
        "questions": [
            {
                "position": 1,
                "prompt": "¿Poco interés o alegría?",
                "required": true,
                "options": [
                    { "position": 1, "label": "Nunca", "contribution": 0 },
                    { "position": 2, "label": "Varios días", "contribution": 1 }
                ]
            },
            {
                "position": 2,
                "prompt": "¿Se ha sentido decaído/a?",
                "required": true,
                "options": [
                    { "position": 1, "label": "Nunca", "contribution": 0 },
                    { "position": 2, "label": "Varios días", "contribution": 1 }
                ]
            }
        ],
        "interpretations": [
            { "label": "Mínima", "description": "Depresión mínima", "lowerBound": 0, "upperBound": 1 },
            { "label": "Leve", "description": "Depresión leve", "lowerBound": 2, "upperBound": 2 }
        ]
    }
    """);

    [Fact]
    public async Task Lifecycle_FullHappyPath_Draft_Ready_Draft_Ready_Published_Retired()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c3_flow")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        // 1. Mark-ready fails if draft is empty/incomplete
        var earlyReadyResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/mark-ready", Json("{}"), accessToken);
        await AssertProblemAsync(earlyReadyResp, HttpStatusCode.BadRequest, "Solicitud no válida", "no contiene ninguna pregunta");

        // 2. Populate complete draft definition
        var putDraftResp = await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition", ValidPsychometricDraftPayload(), accessToken);
        Assert.Equal(HttpStatusCode.OK, putDraftResp.StatusCode);

        // 3. Mark-ready succeeds: DRAFT -> READY
        var markReadyResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/mark-ready", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, markReadyResp.StatusCode);
        using var readyDoc = JsonDocument.Parse(await markReadyResp.Content.ReadAsStringAsync());
        Assert.Equal("READY", readyDoc.RootElement.GetProperty("status").GetString());

        var readyAudits = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_READY");
        Assert.Single(readyAudits);
        Assert.Null(readyAudits[0].Metadata);

        // 4. No-Op Idempotency on mark-ready (200 OK, no new audit)
        var noOpReadyResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/mark-ready", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, noOpReadyResp.StatusCode);
        var readyAuditsAfterNoOp = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_READY");
        Assert.Single(readyAuditsAfterNoOp);

        // 5. Return-to-draft: READY -> DRAFT
        var returnDraftResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/return-to-draft", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, returnDraftResp.StatusCode);
        using var draftDoc = JsonDocument.Parse(await returnDraftResp.Content.ReadAsStringAsync());
        Assert.Equal("DRAFT", draftDoc.RootElement.GetProperty("status").GetString());

        var returnAudits = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_RETURNED_TO_DRAFT");
        Assert.Single(returnAudits);
        Assert.Null(returnAudits[0].Metadata);

        // 6. No-Op Idempotency on return-to-draft (200 OK, no new audit)
        var noOpReturnResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/return-to-draft", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, noOpReturnResp.StatusCode);
        var returnAuditsAfterNoOp = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_RETURNED_TO_DRAFT");
        Assert.Single(returnAuditsAfterNoOp);

        // 7. Transition back to READY
        var reReadyResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/mark-ready", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, reReadyResp.StatusCode);

        // 8. Publish: READY -> PUBLISHED
        var publishResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, publishResp.StatusCode);
        using var pubDoc = JsonDocument.Parse(await publishResp.Content.ReadAsStringAsync());
        Assert.Equal("PUBLISHED", pubDoc.RootElement.GetProperty("status").GetString());

        var pubAudits = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_PUBLISHED");
        Assert.Single(pubAudits);
        Assert.Null(pubAudits[0].Metadata);

        // 9. No-Op Idempotency on publish (200 OK, no new audit)
        var noOpPubResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, noOpPubResp.StatusCode);
        var pubAuditsAfterNoOp = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_PUBLISHED");
        Assert.Single(pubAuditsAfterNoOp);

        // 10. Cannot modify definition or return to draft once published (409 Conflict)
        var putWhilePubResp = await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition", ValidPsychometricDraftPayload(), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, putWhilePubResp.StatusCode);

        var returnWhilePubResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/return-to-draft", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, returnWhilePubResp.StatusCode);

        // 11. Retire: PUBLISHED -> RETIRED
        var retireResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/retire", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, retireResp.StatusCode);
        using var retDoc = JsonDocument.Parse(await retireResp.Content.ReadAsStringAsync());
        Assert.Equal("RETIRED", retDoc.RootElement.GetProperty("status").GetString());

        var retireAudits = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_RETIRED");
        Assert.Single(retireAudits);
        Assert.Null(retireAudits[0].Metadata);

        // 12. No-Op Idempotency on retire (200 OK, no new audit)
        var noOpRetireResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/retire", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, noOpRetireResp.StatusCode);
        var retireAuditsAfterNoOp = await QueryAuditAsync(versionId, "INSTRUMENT_VERSION_RETIRED");
        Assert.Single(retireAuditsAfterNoOp);

        // 13. Terminal state RETIRED cannot transition to anything
        var markReadyFromRetired = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/mark-ready", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, markReadyFromRetired.StatusCode);

        var pubFromRetired = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, pubFromRetired.StatusCode);
    }

    [Fact]
    public async Task MarkReady_WithoutStructuredReferences_ShouldSucceedBecauseSourceDescriptionIsDocumented()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c3_noref")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var verResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var versionId = await ReadGuidPropertyAsync(verResp, "instrumentVersionId");

        // Payload with 0 structured references
        var payloadWithoutReferences = Json("""
        {
            "references": [],
            "questions": [
                {
                    "position": 1,
                    "prompt": "¿Pregunta única?",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "No", "contribution": 0 },
                        { "position": 2, "label": "Sí", "contribution": 1 }
                    ]
                }
            ],
            "interpretations": [
                { "label": "No", "description": "Negativo", "lowerBound": 0, "upperBound": 0 },
                { "label": "Sí", "description": "Positivo", "lowerBound": 1, "upperBound": 1 }
            ]
        }
        """);

        await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition", payloadWithoutReferences, accessToken);

        var markReadyResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/mark-ready", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, markReadyResp.StatusCode);

        var pubResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{versionId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, pubResp.StatusCode);
    }

    [Fact]
    public async Task Publish_WhenAnotherVersionIsAlreadyPublished_ShouldReturn409ConflictAndNotPersistAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c3_dup_pub")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        // Version 1: Create, put definition, mark-ready, publish
        var v1Resp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var v1Id = await ReadGuidPropertyAsync(v1Resp, "instrumentVersionId");
        await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{v1Id}/draft-definition", ValidPsychometricDraftPayload(), accessToken);
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v1Id}/mark-ready", Json("{}"), accessToken);
        var pubV1Resp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v1Id}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, pubV1Resp.StatusCode);

        // Version 2: Create, put definition, mark-ready
        var v2Resp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(2), accessToken);
        var v2Id = await ReadGuidPropertyAsync(v2Resp, "instrumentVersionId");
        await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{v2Id}/draft-definition", ValidPsychometricDraftPayload(), accessToken);
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v2Id}/mark-ready", Json("{}"), accessToken);

        // Attempting to publish Version 2 while Version 1 is still PUBLISHED must fail with 409
        var pubV2Resp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v2Id}/publish", Json("{}"), accessToken);
        await AssertProblemAsync(pubV2Resp, HttpStatusCode.Conflict, "Conflicto", "Debe retirar la versión publicada existente antes de publicar una nueva");

        // Verify v2 remains READY and no PUBLISHED audit was recorded for v2
        var v2Audits = await QueryAuditAsync(v2Id, "INSTRUMENT_VERSION_PUBLISHED");
        Assert.Empty(v2Audits);

        // Explicit retirement of Version 1 enables publishing Version 2
        var retireV1Resp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v1Id}/retire", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, retireV1Resp.StatusCode);

        var pubV2SuccessResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v2Id}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, pubV2SuccessResp.StatusCode);
    }

    [Fact]
    public async Task Publish_ConcurrentRequestsOnDifferentVersions_ShouldEnforceUniquePublishedConstraint()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var instResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments", CreateInstrumentBody(NewCode("c3_conc_pub")), accessToken);
        var instrumentId = await ReadGuidPropertyAsync(instResp, "instrumentId");

        var v1Resp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(1), accessToken);
        var v1Id = await ReadGuidPropertyAsync(v1Resp, "instrumentVersionId");
        await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{v1Id}/draft-definition", ValidPsychometricDraftPayload(), accessToken);
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v1Id}/mark-ready", Json("{}"), accessToken);

        var v2Resp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions", CreateVersionBody(2), accessToken);
        var v2Id = await ReadGuidPropertyAsync(v2Resp, "instrumentVersionId");
        await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instrumentId}/versions/{v2Id}/draft-definition", ValidPsychometricDraftPayload(), accessToken);
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v2Id}/mark-ready", Json("{}"), accessToken);

        var task1 = SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v1Id}/publish", Json("{}"), accessToken);
        var task2 = SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instrumentId}/versions/{v2Id}/publish", Json("{}"), accessToken);

        var responses = await Task.WhenAll(task1, task2);
        var statuses = responses.Select(r => r.StatusCode).ToList();

        // Exactly one should succeed (200 OK) and the other should fail with 409 Conflict
        Assert.Contains(HttpStatusCode.OK, statuses);
        Assert.Contains(HttpStatusCode.Conflict, statuses);

        // Verify only 1 version is PUBLISHED in the database
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var publishedCount = await connection.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.instrument_version WHERE instrument_id = @InstrumentId AND status = 'PUBLISHED'",
            new { InstrumentId = instrumentId });
        Assert.Equal(1, publishedCount);
    }

    // ------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------

    private static string NewCode(string label) => $"c1_{label}_{Guid.NewGuid():N}"[..30];

    private static HttpContent CreateInstrumentBody(
        string code,
        string name = "PHQ-9 Depresión",
        string description = "Cuestionario de 9 ítems.",
        string purpose = "Despistaje en atención primaria.") =>
        JsonContent.Create(new { code, name, description, purpose });

    private static HttpContent CreateVersionBody(
        int? version,
        string sourceDescription = "Kroenke K. 2001.",
        string population = "Adultos mayores de 18 años.",
        string? administrationConditions = "Ambiente tranquilo.",
        string? licenseInformation = "Dominio público.",
        string limitations = "No constituye diagnóstico clínico formal.") =>
        JsonContent.Create(new
        {
            version,
            sourceDescription,
            population,
            administrationConditions,
            licenseInformation,
            limitations
        });

    private static HttpContent Json(string rawJson) =>
        new StringContent(rawJson, Encoding.UTF8, "application/json");

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        HttpContent content,
        string? accessToken = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

    private static Task<HttpResponseMessage> SendGetAsync(
        HttpClient client, string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }

    private static async Task<Guid> ReadGuidPropertyAsync(HttpResponseMessage response, string propertyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty(propertyName).GetGuid();
    }

    private static string[] ReadChangedFields(string? metadata)
    {
        Assert.False(string.IsNullOrWhiteSpace(metadata));
        using var document = JsonDocument.Parse(metadata!);
        return document.RootElement.GetProperty("changed_fields")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedDetailFragment)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.Contains(expectedDetailFragment, problem.Detail!, StringComparison.Ordinal);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private WebApplicationFactory<Program> CreateFactory(
        Action<IServiceCollection>? configureServices = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
            builder.ConfigureServices(services => configureServices?.Invoke(services));
        });

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync()
    {
        string email = $"instrument_admin_{Guid.NewGuid():N}@yusay.local";
        var registration = await new RegisterUserUseCase(
                _unitOfWork, _userAccountRepo, _userCredentialRepo, _verificationTokenRepo,
                _auditEventRepo, _passwordHasher, new SecureTokenService(), new NullEmailVerificationSender())
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

    private sealed class AuditRow
    {
        public string Action { get; init; } = string.Empty;
        public string Actor_kind { get; init; } = string.Empty;
        public Guid? Actor_user_id { get; init; }
        public string Target_type { get; init; } = string.Empty;
        public string Target_identifier { get; init; } = string.Empty;
        public string? Metadata { get; init; }
    }

    private async Task<AuditRow[]> QueryAuditAsync(Guid targetId, string? action = null)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var rows = await connection.QueryAsync<AuditRow>(new CommandDefinition(action is null
            ? """
              SELECT action, actor_kind, actor_user_id, target_type, target_identifier,
                     metadata::text AS metadata
              FROM yusay.audit_event
              WHERE target_identifier = @TargetIdentifier
              ORDER BY occurred_at, audit_event_id
              """
            : """
              SELECT action, actor_kind, actor_user_id, target_type, target_identifier,
                     metadata::text AS metadata
              FROM yusay.audit_event
              WHERE target_identifier = @TargetIdentifier AND action = @Action
              ORDER BY occurred_at, audit_event_id
              """, new { TargetIdentifier = targetId.ToString(), Action = action }));
        return rows.ToArray();
    }

    private async Task<int> CountScoringDefinitionsAsync(Guid versionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.scoring_definition WHERE instrument_version_id = @VersionId",
            new { VersionId = versionId }));
    }

    private sealed class UnreachableAdministratorRepository : IAdministratorAuthorizationRepository
    {
        public Task<bool> IsAdministratorAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Simulación de PostgreSQL inaccesible durante la comprobación administrativa.");
    }
}
