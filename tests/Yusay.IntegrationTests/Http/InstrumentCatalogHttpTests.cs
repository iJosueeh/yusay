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
public sealed class InstrumentCatalogHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-instrument-catalog-http-integration-test-secret-0123456789";

    private const string ValidPassword = "NormativePassword#2026";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public InstrumentCatalogHttpTests(PostgreSqlFixture fixture)
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

    [Fact]
    public async Task Endpoints_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var listResp = await SendGetAsync(client, "/instruments");
        await AssertProblemAsync(listResp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var getResp = await SendGetAsync(client, $"/instruments/{Guid.NewGuid()}");
        await AssertProblemAsync(getResp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task List_WithStandardUser_ShouldReturnOnlyPublishedInstrumentsSortedByNameAscAndInstrumentIdAsc()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, standardToken) = await CreateSessionAsync();

        // 1. Instrument with DRAFT only
        var draftInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_draft"), "Instrumento Draft");
        var draftVerId = await CreateVersionAsync(client, adminToken, draftInst, 1);

        // 2. Instrument with READY only
        var readyInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_ready"), "Instrumento Ready");
        var readyVerId = await CreateVersionAsync(client, adminToken, readyInst, 1);
        await PutValidCompositionAsync(client, adminToken, readyInst, readyVerId);
        await MarkReadyAsync(client, adminToken, readyInst, readyVerId);

        // 3. Instrument Zeta with PUBLISHED
        var zetaInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_zeta"), "Zeta Instrumento");
        var zetaVerId = await CreateVersionAsync(client, adminToken, zetaInst, 1);
        await PutValidCompositionAsync(client, adminToken, zetaInst, zetaVerId);
        await MarkReadyAsync(client, adminToken, zetaInst, zetaVerId);
        await PublishAsync(client, adminToken, zetaInst, zetaVerId);

        // 4. Instrument Alpha with PUBLISHED
        var alphaInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_alpha"), "Alpha Instrumento");
        var alphaVerId = await CreateVersionAsync(client, adminToken, alphaInst, 1);
        await PutValidCompositionAsync(client, adminToken, alphaInst, alphaVerId);
        await MarkReadyAsync(client, adminToken, alphaInst, alphaVerId);
        await PublishAsync(client, adminToken, alphaInst, alphaVerId);

        // 5. Instrument Retired
        var retiredInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_retired"), "Instrumento Retirado");
        var retiredVerId = await CreateVersionAsync(client, adminToken, retiredInst, 1);
        await PutValidCompositionAsync(client, adminToken, retiredInst, retiredVerId);
        await MarkReadyAsync(client, adminToken, retiredInst, retiredVerId);
        await PublishAsync(client, adminToken, retiredInst, retiredVerId);
        await RetireAsync(client, adminToken, retiredInst, retiredVerId);

        // Query catalog as standard authenticated user
        var listResponse = await SendGetAsync(client, "/instruments", standardToken);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var rawJson = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();

        var itemIds = items.Select(i => i.GetProperty("instrumentId").GetGuid()).ToList();
        Assert.Contains(zetaInst, itemIds);
        Assert.Contains(alphaInst, itemIds);
        Assert.DoesNotContain(draftInst, itemIds);
        Assert.DoesNotContain(readyInst, itemIds);
        Assert.DoesNotContain(retiredInst, itemIds);

        // Check deterministic sorting: Alpha appears before Zeta
        var alphaIndex = itemIds.IndexOf(alphaInst);
        var zetaIndex = itemIds.IndexOf(zetaInst);
        Assert.True(alphaIndex < zetaIndex);

        // Check publishedVersion fields
        var alphaItem = items.First(i => i.GetProperty("instrumentId").GetGuid() == alphaInst);
        var pubVer = alphaItem.GetProperty("publishedVersion");
        Assert.Equal(alphaVerId, pubVer.GetProperty("instrumentVersionId").GetGuid());
        Assert.Equal(1, pubVer.GetProperty("version").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(pubVer.GetProperty("sourceDescription").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(pubVer.GetProperty("population").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(pubVer.GetProperty("limitations").GetString()));

        // Verify total absence of internal psychometric fields in list response
        Assert.DoesNotContain("questions", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("options", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contribution", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scoring", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interpretations", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lowerBound", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("upperBound", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_WhenPublished_ShouldReturnDetailsAndReferencesWithoutInternalData()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, standardToken) = await CreateSessionAsync();

        var code = NewCode("cat_det");
        var instId = await CreateInstrumentAsync(client, adminToken, code, "Instrumento Detalle", "Descripción Detalle", "Propósito Detalle");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1, "Kroenke 2001", "Población Detalle", "Condiciones Detalle", "Licencia Detalle", "Limitaciones Detalle");

        await PutValidCompositionAsync(client, adminToken, instId, verId);
        await MarkReadyAsync(client, adminToken, instId, verId);
        await PublishAsync(client, adminToken, instId, verId);

        // Act: Standard user requests details
        var getResp = await SendGetAsync(client, $"/instruments/{instId}", standardToken);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        var rawJson = await getResp.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        Assert.Equal(instId, root.GetProperty("instrumentId").GetGuid());
        Assert.Equal(code, root.GetProperty("code").GetString());
        Assert.Equal("Instrumento Detalle", root.GetProperty("name").GetString());
        Assert.Equal("Descripción Detalle", root.GetProperty("description").GetString());
        Assert.Equal("Propósito Detalle", root.GetProperty("purpose").GetString());

        var ver = root.GetProperty("version");
        Assert.Equal(verId, ver.GetProperty("instrumentVersionId").GetGuid());
        Assert.Equal(1, ver.GetProperty("version").GetInt32());
        Assert.Equal("Kroenke 2001", ver.GetProperty("sourceDescription").GetString());
        Assert.Equal("Población Detalle", ver.GetProperty("population").GetString());
        Assert.Equal("Condiciones Detalle", ver.GetProperty("administrationConditions").GetString());
        Assert.Equal("Licencia Detalle", ver.GetProperty("licenseInformation").GetString());
        Assert.Equal("Limitaciones Detalle", ver.GetProperty("limitations").GetString());

        // References check
        var references = ver.GetProperty("references").EnumerateArray().ToList();
        Assert.Single(references);
        Assert.Equal(1, references[0].GetProperty("referenceOrder").GetInt32());
        Assert.Equal("Kroenke K. 2001.", references[0].GetProperty("citation").GetString());
        Assert.Equal("https://doi.org/10.1046/phq9", references[0].GetProperty("url").GetString());

        // Strict assertion: zero psychometric internal fields
        Assert.DoesNotContain("questions", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("options", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contribution", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scoring", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interpretations", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lowerBound", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("upperBound", rawJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetById_WhenNotPublishedOrNonExistent_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, standardToken) = await CreateSessionAsync();

        // 1. Non-existent GUID
        var missingResp = await SendGetAsync(client, $"/instruments/{Guid.NewGuid()}", standardToken);
        await AssertProblemAsync(missingResp, HttpStatusCode.NotFound, "Recurso no encontrado", "no existe o no tiene una versión publicada");

        // 2. Draft only
        var draftInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_404_draft"));
        await CreateVersionAsync(client, adminToken, draftInst, 1);
        var draftResp = await SendGetAsync(client, $"/instruments/{draftInst}", standardToken);
        await AssertProblemAsync(draftResp, HttpStatusCode.NotFound, "Recurso no encontrado", "no existe o no tiene una versión publicada");

        // 3. Ready only
        var readyInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_404_ready"));
        var readyVerId = await CreateVersionAsync(client, adminToken, readyInst, 1);
        await PutValidCompositionAsync(client, adminToken, readyInst, readyVerId);
        await MarkReadyAsync(client, adminToken, readyInst, readyVerId);
        var readyResp = await SendGetAsync(client, $"/instruments/{readyInst}", standardToken);
        await AssertProblemAsync(readyResp, HttpStatusCode.NotFound, "Recurso no encontrado", "no existe o no tiene una versión publicada");

        // 4. Retired only
        var retiredInst = await CreateInstrumentAsync(client, adminToken, NewCode("cat_404_ret"));
        var retiredVerId = await CreateVersionAsync(client, adminToken, retiredInst, 1);
        await PutValidCompositionAsync(client, adminToken, retiredInst, retiredVerId);
        await MarkReadyAsync(client, adminToken, retiredInst, retiredVerId);
        await PublishAsync(client, adminToken, retiredInst, retiredVerId);
        await RetireAsync(client, adminToken, retiredInst, retiredVerId);
        var retiredResp = await SendGetAsync(client, $"/instruments/{retiredInst}", standardToken);
        await AssertProblemAsync(retiredResp, HttpStatusCode.NotFound, "Recurso no encontrado", "no existe o no tiene una versión publicada");
    }

    [Fact]
    public async Task Catalog_WhenVersionIsRetired_ShouldImmediatelyDisappearAndReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, standardToken) = await CreateSessionAsync();

        var instId = await CreateInstrumentAsync(client, adminToken, NewCode("cat_dyn_ret"), "Retiro Dinámico");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);
        await PutValidCompositionAsync(client, adminToken, instId, verId);
        await MarkReadyAsync(client, adminToken, instId, verId);
        await PublishAsync(client, adminToken, instId, verId);

        // Active in catalog
        var activeResp = await SendGetAsync(client, $"/instruments/{instId}", standardToken);
        Assert.Equal(HttpStatusCode.OK, activeResp.StatusCode);

        // Admin retires the published version
        await RetireAsync(client, adminToken, instId, verId);

        // Next read immediately sees retired state under MVCC and returns 404
        var afterRetireResp = await SendGetAsync(client, $"/instruments/{instId}", standardToken);
        await AssertProblemAsync(afterRetireResp, HttpStatusCode.NotFound, "Recurso no encontrado", "no existe o no tiene una versión publicada");

        // And list no longer contains the instrument
        var listResp = await SendGetAsync(client, "/instruments", standardToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        using var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var ids = listDoc.RootElement.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("instrumentId").GetGuid())
            .ToList();
        Assert.DoesNotContain(instId, ids);
    }

    // ------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------

    private static string NewCode(string label) => $"c4_{label}_{Guid.NewGuid():N}"[..30];

    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient client,
        string path,
        string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

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

    private static HttpContent Json(string rawJson) =>
        new StringContent(rawJson, Encoding.UTF8, "application/json");

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync()
    {
        string email = $"instrument_catalog_{Guid.NewGuid():N}@yusay.local";
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

    private async Task GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
    }

    private static async Task<Guid> CreateInstrumentAsync(
        HttpClient client,
        string accessToken,
        string code,
        string name = "Nombre",
        string description = "Descripción",
        string purpose = "Propósito")
    {
        var resp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            JsonContent.Create(new { code, name, description, purpose }),
            accessToken);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("instrumentId").GetGuid();
    }

    private static async Task<Guid> CreateVersionAsync(
        HttpClient client,
        string accessToken,
        Guid instrumentId,
        int version,
        string sourceDescription = "Fuente",
        string population = "Población",
        string? administrationConditions = null,
        string? licenseInformation = null,
        string limitations = "Limitaciones")
    {
        var resp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions",
            JsonContent.Create(new
            {
                version,
                sourceDescription,
                population,
                administrationConditions,
                licenseInformation,
                limitations
            }),
            accessToken);
        resp.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("instrumentVersionId").GetGuid();
    }

    private static async Task PutValidCompositionAsync(
        HttpClient client,
        string accessToken,
        Guid instrumentId,
        Guid versionId)
    {
        var payload = Json("""
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
                }
            ],
            "interpretations": [
                { "label": "Mínima", "description": "Depresión mínima", "lowerBound": 0, "upperBound": 0 },
                { "label": "Leve", "description": "Depresión leve", "lowerBound": 1, "upperBound": 1 }
            ]
        }
        """);

        var resp = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/admin/instruments/{instrumentId}/versions/{versionId}/draft-definition",
            payload,
            accessToken);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task MarkReadyAsync(
        HttpClient client,
        string accessToken,
        Guid instrumentId,
        Guid versionId)
    {
        var resp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions/{versionId}/mark-ready",
            Json("{}"),
            accessToken);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task PublishAsync(
        HttpClient client,
        string accessToken,
        Guid instrumentId,
        Guid versionId)
    {
        var resp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions/{versionId}/publish",
            Json("{}"),
            accessToken);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task RetireAsync(
        HttpClient client,
        string accessToken,
        Guid instrumentId,
        Guid versionId)
    {
        var resp = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instrumentId}/versions/{versionId}/retire",
            Json("{}"),
            accessToken);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string? expectedDetailSubstring = null)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));

        if (expectedDetailSubstring is not null)
        {
            Assert.Contains(expectedDetailSubstring, problem.Detail!, StringComparison.OrdinalIgnoreCase);
        }
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
}
