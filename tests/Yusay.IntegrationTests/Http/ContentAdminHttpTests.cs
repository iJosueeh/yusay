using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Yusay.Application.Common.Interfaces;
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
public sealed class ContentAdminHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret = "yusay-content-admin-integration-test-secret-0123456789";
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

    public ContentAdminHttpTests(PostgreSqlFixture fixture)
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
    // 1. Autorización: 401 sin token, 403 usuario no admin, 503 fail-closed
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Endpoints_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resPostTopic = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json("""{"code":"t1","name":"T1"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, resPostTopic.StatusCode);

        var resGetTopics = await SendGetAsync(client, "/admin/topics");
        Assert.Equal(HttpStatusCode.Unauthorized, resGetTopics.StatusCode);

        var resPostResource = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources", Json("""{"type":"ARTICLE","title":"T1"}"""));
        Assert.Equal(HttpStatusCode.Unauthorized, resPostResource.StatusCode);

        var resGetResources = await SendGetAsync(client, "/admin/resources");
        Assert.Equal(HttpStatusCode.Unauthorized, resGetResources.StatusCode);
    }

    [Fact]
    public async Task Endpoints_WithNonAdmin_ShouldReturn403()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var resPostTopic = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json("""{"code":"t_no_admin","name":"T"}"""), accessToken);
        Assert.Equal(HttpStatusCode.Forbidden, resPostTopic.StatusCode);

        var resPostResource = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources", Json("""{"type":"ARTICLE","title":"T"}"""), accessToken);
        Assert.Equal(HttpStatusCode.Forbidden, resPostResource.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // 2. Topics: Creación, duplicados y listado
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateTopic_WithValidData_ShouldReturn201AndPersistAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var code = NewCode("topic");
        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/topics",
            Json($$"""{"code":"{{code}}","name":"Manejo del Estrés","description":"Técnicas de regulación"}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var topicId = doc.RootElement.GetProperty("topicId").GetGuid();
        Assert.NotEqual(Guid.Empty, topicId);
        Assert.Equal(code, doc.RootElement.GetProperty("code").GetString());
        Assert.Equal("Manejo del Estrés", doc.RootElement.GetProperty("name").GetString());

        var audit = Assert.Single(await QueryAuditAsync(topicId));
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("TOPIC", audit.Target_type);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task CreateTopic_WithDuplicateCode_ShouldReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var code = NewCode("dup_topic");
        var res1 = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{code}}","name":"T1"}"""), accessToken);
        Assert.Equal(HttpStatusCode.Created, res1.StatusCode);

        var res2 = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{code}}","name":"T2"}"""), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, res2.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // 3. Resources: Creación atómica con topics y auditoría Perfiles N y T
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task CreateResource_WithTopics_ShouldBeAtomicAndAuditProfilesNAndT()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var topicCode = NewCode("res_t");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"Ansiedad"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""
            {
                "type": "ARTICLE",
                "title": "Guía de autocuidado",
                "summary": "Pautas diarias para el bienestar.",
                "body": "El autocuidado incluye descanso regular y pausas conscientes.",
                "topicIds": ["{{topicId}}"]
            }
            """),
            accessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();
        Assert.NotEqual(Guid.Empty, resourceId);
        Assert.Equal("DRAFT", doc.RootElement.GetProperty("status").GetString());
        Assert.Equal("ARTICLE", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal("Guía de autocuidado", doc.RootElement.GetProperty("title").GetString());

        var topicsArray = doc.RootElement.GetProperty("topics");
        Assert.Equal(1, topicsArray.GetArrayLength());
        Assert.Equal(topicId, topicsArray[0].GetProperty("topicId").GetGuid());

        var audits = await QueryAuditAsync(resourceId);
        Assert.Equal(2, audits.Length);

        var createdAudit = Assert.Single(audits, a => a.Action == "CATALOG_CREATED");
        Assert.Equal("ADMINISTRATOR", createdAudit.Actor_kind);
        Assert.Equal(adminUserId, createdAudit.Actor_user_id);
        Assert.Equal("RESOURCE", createdAudit.Target_type);
        Assert.Null(createdAudit.Metadata);

        var assocAudit = Assert.Single(audits, a => a.Action == "EDITORIAL_ASSOCIATION_ADDED");
        Assert.Equal("ADMINISTRATOR", assocAudit.Actor_kind);
        Assert.Equal(adminUserId, assocAudit.Actor_user_id);
        Assert.Equal("RESOURCE", assocAudit.Target_type);
        Assert.NotNull(assocAudit.Metadata);
        Assert.Contains(topicId.ToString(), assocAudit.Metadata);
    }

    // ------------------------------------------------------------------------------------------
    // 4. Publicación: validaciones de tipo (ARTICLE vs EXTERNAL_LINK) y requisito de temas
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Publish_ArticleWithoutBody_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        // Crear draft sin body
        var createRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources", Json("""{"type":"ARTICLE","title":"Borrador sin cuerpo"}"""), accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();

        // Intentar publicar
        var pubRes = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.BadRequest, pubRes.StatusCode);
    }

    [Fact]
    public async Task Publish_ExternalLinkWithInsecureHttp_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json("""{"type":"EXTERNAL_LINK","title":"Enlace no seguro","externalUrl":"http://insecure.example.com"}"""),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();

        var pubRes = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.BadRequest, pubRes.StatusCode);
    }

    [Fact]
    public async Task Publish_ValidArticleWithTopic_ShouldReturn200AndAuditResourcePublished()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var topicCode = NewCode("pub_t");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"Sueño"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""
            {
                "type": "ARTICLE",
                "title": "Higiene del sueño",
                "body": "Consejos para dormir 8 horas diarias de manera saludable.",
                "topicIds": ["{{topicId}}"]
            }
            """),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();

        var pubRes = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, pubRes.StatusCode);

        using var pubDoc = JsonDocument.Parse(await pubRes.Content.ReadAsStringAsync());
        Assert.Equal("PUBLISHED", pubDoc.RootElement.GetProperty("status").GetString());

        var audit = Assert.Single(await QueryAuditAsync(resourceId, "RESOURCE_PUBLISHED"));
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("RESOURCE", audit.Target_type);
        Assert.Null(audit.Metadata);
    }

    // ------------------------------------------------------------------------------------------
    // 5. Modificación: DRAFT (libre) vs PUBLISHED (corrección menor Profile E) vs Inmutabilidad de tipo
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task UpdatePublishedResource_WithoutCorrectionKind_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var topicCode = NewCode("t_corr");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"T"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Artículo","body":"Cuerpo","topicIds":["{{topicId}}"]}"""),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);

        // Actualizar sin correctionKind
        var updateRes = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/admin/resources/{resourceId}",
            Json("""{"title":"Artículo corregido","body":"Cuerpo corregido"}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.BadRequest, updateRes.StatusCode);
    }

    [Fact]
    public async Task UpdatePublishedResource_ChangingType_ShouldReturn409Conflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var topicCode = NewCode("t_type");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"T"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Artículo","body":"Cuerpo","topicIds":["{{topicId}}"]}"""),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);

        // Intentar cambiar tipo a EXTERNAL_LINK
        var updateRes = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/admin/resources/{resourceId}",
            Json("""{"type":"EXTERNAL_LINK","title":"Artículo","externalUrl":"https://example.com","correctionKind":"FORMAT"}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.Conflict, updateRes.StatusCode);
    }

    [Fact]
    public async Task UpdatePublishedResource_ValidMinorCorrection_ShouldReturn200AndAuditProfileE()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var topicCode = NewCode("t_valid_corr");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"T"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Artículo con error ortografico","body":"Cuerpo inicial","topicIds":["{{topicId}}"]}"""),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);

        var updateRes = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/admin/resources/{resourceId}",
            Json("""{"title":"Artículo con error ortográfico","body":"Cuerpo inicial","correctionKind":"SPELLING"}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var audit = Assert.Single(await QueryAuditAsync(resourceId, "CATALOG_UPDATED"));
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("RESOURCE", audit.Target_type);
        Assert.NotNull(audit.Metadata);
        Assert.Contains("SPELLING", audit.Metadata);
        Assert.Contains("title", audit.Metadata);
    }

    // ------------------------------------------------------------------------------------------
    // 6. Temas en PUBLISHED: Prohibición de eliminar el último tema
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task RemoveTopic_WhenPublishedWithSingleTopic_ShouldReturn409Conflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var topicCode = NewCode("t_single");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"T"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Art","body":"Cuerpo","topicIds":["{{topicId}}"]}"""),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();
        await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);

        var removeRes = await SendAsync(client, HttpMethod.Delete, $"/admin/resources/{resourceId}/topics/{topicId}", accessToken);
        Assert.Equal(HttpStatusCode.Conflict, removeRes.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // 7. Retiro: solo desde PUBLISHED, RETIRED terminal
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Retire_DirectlyFromDraft_ShouldReturn409Conflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var createRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources", Json("""{"type":"ARTICLE","title":"Borrador"}"""), accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();

        var retireRes = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/retire", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, retireRes.StatusCode);
    }

    [Fact]
    public async Task Retire_FromPublished_ShouldSucceedAndBeTerminal()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var topicCode = NewCode("t_retire");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"T"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Art","body":"Cuerpo","topicIds":["{{topicId}}"]}"""),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();

        await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);

        // Retirar
        var retireRes = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/retire", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.OK, retireRes.StatusCode);

        var audit = Assert.Single(await QueryAuditAsync(resourceId, "RESOURCE_RETIRED"));
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Null(audit.Metadata);

        // Intentar publicar desde RETIRED -> Debe fallar 409
        var republishRes = await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, republishRes.StatusCode);

        // Intentar editar desde RETIRED -> Debe fallar 409
        var reeditRes = await SendJsonAsync(client, HttpMethod.Put, $"/admin/resources/{resourceId}", Json("""{"title":"Editado","correctionKind":"SPELLING"}"""), accessToken);
        Assert.Equal(HttpStatusCode.Conflict, reeditRes.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // 8. Concurrencia: Publicación vs eliminación concurrente del último tema
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Concurrency_PublishVsRemoveLastTopic_PreservesIntegrity()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var topicCode = NewCode("t_race");
        var topicRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/topics", Json($$"""{"code":"{{topicCode}}","name":"T"}"""), accessToken);
        using var topicDoc = JsonDocument.Parse(await topicRes.Content.ReadAsStringAsync());
        var topicId = topicDoc.RootElement.GetProperty("topicId").GetGuid();

        var createRes = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Concurrente","body":"Cuerpo","topicIds":["{{topicId}}"]}"""),
            accessToken);
        using var doc = JsonDocument.Parse(await createRes.Content.ReadAsStringAsync());
        var resourceId = doc.RootElement.GetProperty("resourceId").GetGuid();

        var publishTask = SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), accessToken);
        var removeTopicTask = SendAsync(client, HttpMethod.Delete, $"/admin/resources/{resourceId}/topics/{topicId}", accessToken);

        await Task.WhenAll(publishTask, removeTopicTask);

        // Verificar estado final: NUNCA puede haber un recurso PUBLISHED con 0 topics
        var getRes = await SendGetAsync(client, $"/admin/resources/{resourceId}", accessToken);
        using var finalDoc = JsonDocument.Parse(await getRes.Content.ReadAsStringAsync());
        var status = finalDoc.RootElement.GetProperty("status").GetString();
        var topicsLength = finalDoc.RootElement.GetProperty("topics").GetArrayLength();

        if (status == "PUBLISHED")
        {
            Assert.True(topicsLength >= 1, "Un recurso PUBLISHED nunca puede quedar con 0 topics.");
        }
    }

    // ------------------------------------------------------------------------------------------
    // Helpers de infraestructura y sesión
    // ------------------------------------------------------------------------------------------

    private static string NewCode(string prefix) =>
        $"{prefix}_{Guid.NewGuid():N}"[..Math.Min(32, $"{prefix}_{Guid.NewGuid():N}".Length)];

    private static StringContent Json(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        HttpContent content,
        string? bearerToken = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        string? bearerToken = null)
    {
        using var request = new HttpRequestMessage(method, url);
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient client,
        string url,
        string? bearerToken = null) =>
        await SendAsync(client, HttpMethod.Get, url, bearerToken);

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
        var email = $"content-admin-{Guid.NewGuid():N}@test.yusay.local";
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
}
