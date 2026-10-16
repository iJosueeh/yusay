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

/// <summary>
/// Pruebas HTTP de las lecturas del catálogo de Dimensions (B4 de RF-021): 401 sin token
/// o con token inválido, visibilidad de estados por ruta (solo ACTIVE en la lista de
/// usuario, ACTIVE/RETIRED en el detalle y DRAFT únicamente en /admin/dimensions), 404
/// uniforme para dimensión inexistente y para dimensión con solo versiones DRAFT, 403
/// ProblemDetails con traceId ante identidad sin habilitación administrativa (OQ-ARCH-017),
/// 503 fail-closed cuando la comprobación administrativa no puede ejecutarse y revocación
/// observable en la siguiente evaluación. Sin acceso anónimo (OQ-PROD-002 sigue OPEN) y
/// sin auditoría de lecturas en este bloque. Las aserciones se filtran por los IDs
/// sembrados porque la base de la colección es compartida.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class DimensionHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-dimension-http-integration-test-secret-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private const string TokenRejectedMessage =
        "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public DimensionHttpTests(PostgreSqlFixture fixture)
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
    // Autenticación: las tres rutas son privadas (OQ-PROD-002 sigue OPEN, sin anónimos)
    // ------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("/dimensions")]
    [InlineData("/admin/dimensions")]
    public async Task List_WithoutToken_ShouldReturn401ProblemDetails(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendGetAsync(client, path);

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Theory]
    [InlineData("/dimensions")]
    [InlineData("/admin/dimensions")]
    public async Task List_WithInvalidToken_ShouldReturn401ProblemDetails(string path)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendGetAsync(client, path, $"invalid_{Guid.NewGuid():N}");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado", TokenRejectedMessage);
    }

    [Fact]
    public async Task GetById_WithoutToken_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendGetAsync(client, $"/dimensions/{Guid.NewGuid()}");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    // ------------------------------------------------------------------------------------------
    // GET /dimensions: solo versiones ACTIVE, ocultas las DRAFT y las RETIRED
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_WithValidSession_ShouldExposeOnlyActiveVersionsOrderedByCode()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var zulu = await SeedDimensionAsync("zulu", ("RETIRED", 1), ("ACTIVE", 2), ("DRAFT", 3));
        var alpha = await SeedDimensionAsync("alpha", ("ACTIVE", 1));
        var draftOnly = await SeedDimensionAsync("draftonly", ("DRAFT", 1));

        var response = await SendGetAsync(client, "/dimensions", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await ReadItemsAsync(response);
        var mine = items.Where(item => IsOneOf(item, zulu, alpha, draftOnly)).ToArray();

        // Las dimensiones sin versión ACTIVE no aparecen; el orden es code ASC
        Assert.Equal(new[] { alpha, zulu }, mine.Select(GetDimensionId));

        var zuluItem = SingleItem(mine, zulu);
        var version = Assert.Single(GetVersions(zuluItem));
        Assert.Equal("ACTIVE", version.GetProperty("status").GetString());
        Assert.Equal(2, version.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task List_WithValidSession_ShouldNeverExposeDraftVersions()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var dimensionId = await SeedDimensionAsync("status", ("RETIRED", 1), ("ACTIVE", 2), ("DRAFT", 3));

        var response = await SendGetAsync(client, "/dimensions", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var item = SingleItem(await ReadItemsAsync(response), dimensionId);
        Assert.Equal(
            new[] { "ACTIVE" },
            GetVersions(item).Select(version => version.GetProperty("status").GetString()));
    }

    // ------------------------------------------------------------------------------------------
    // GET /dimensions/{dimensionId}: ACTIVE y RETIRED con definition, escala y anclas
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetById_WithValidSession_ShouldReturnActiveAndRetiredVersionsWithoutDraft()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var dimensionId = await SeedDimensionAsync("history", ("RETIRED", 1), ("ACTIVE", 2), ("DRAFT", 3));

        var response = await SendGetAsync(client, $"/dimensions/{dimensionId}", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = body.RootElement;

        Assert.Equal(dimensionId, root.GetProperty("dimensionId").GetGuid());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("code").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("name").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("description").GetString()));

        var versions = root.GetProperty("versions").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 2 }, versions.Select(version => version.GetProperty("version").GetInt32()));
        Assert.Equal("RETIRED", versions[0].GetProperty("status").GetString());
        Assert.Equal("ACTIVE", versions[1].GetProperty("status").GetString());

        // Definición, escala y anclas de la versión histórica, con anclas en value ASC
        Assert.Equal("Definición histórica de prueba.", versions[0].GetProperty("definition").GetString());
        Assert.Equal((1, 5, 1), (
            versions[0].GetProperty("minValue").GetInt32(),
            versions[0].GetProperty("maxValue").GetInt32(),
            versions[0].GetProperty("step").GetInt32()));

        var anchors = versions[0].GetProperty("anchors").EnumerateArray().ToArray();
        Assert.Equal(new[] { 1, 5 }, anchors.Select(anchor => anchor.GetProperty("value").GetInt32()));
        Assert.Equal("Ancla mínima", anchors[0].GetProperty("label").GetString());
        Assert.Equal("Ancla máxima", anchors[1].GetProperty("label").GetString());
    }

    [Fact]
    public async Task GetById_ForNonexistentAndOnlyDraftDimensions_ShouldReturnUniform404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var draftOnly = await SeedDimensionAsync("draftonly404", ("DRAFT", 1));

        var nonexistentResponse = await SendGetAsync(client, $"/dimensions/{Guid.NewGuid()}", accessToken);
        var draftOnlyResponse = await SendGetAsync(client, $"/dimensions/{draftOnly}", accessToken);

        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, draftOnlyResponse.StatusCode);

        // 404 uniforme: los campos públicos de ProblemDetails son idénticos salvo traceId
        using var nonexistent = JsonDocument.Parse(await nonexistentResponse.Content.ReadAsStringAsync());
        using var draftOnlyBody = JsonDocument.Parse(await draftOnlyResponse.Content.ReadAsStringAsync());
        foreach (var field in new[] { "title", "detail", "status", "type" })
        {
            Assert.True(nonexistent.RootElement.TryGetProperty(field, out var nonexistentValue),
                $"Falta el campo público {field} en el 404 de dimensión inexistente.");
            Assert.True(draftOnlyBody.RootElement.TryGetProperty(field, out var draftOnlyValue),
                $"Falta el campo público {field} en el 404 de dimensión con solo DRAFT.");
            Assert.Equal(nonexistentValue.GetRawText(), draftOnlyValue.GetRawText());
        }
        Assert.NotEqual(
            nonexistent.RootElement.GetProperty("traceId").GetString(),
            draftOnlyBody.RootElement.GetProperty("traceId").GetString());
    }

    // ------------------------------------------------------------------------------------------
    // GET /admin/dimensions: política Administrator (OQ-ARCH-017), DRAFT incluido
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AdminList_WithAuthenticatedNonAdministrator_ShouldReturn403ProblemDetailsWithTraceId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var response = await SendGetAsync(client, "/admin/dimensions", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");
    }

    [Fact]
    public async Task AdminList_WithAdministrator_ShouldIncludeDraftsAndDimensionsWithoutVersions()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var mixed = await SeedDimensionAsync("adm_mixed", ("RETIRED", 1), ("ACTIVE", 2), ("DRAFT", 3));
        var pending = await SeedDimensionAsync("adm_pending");

        var response = await SendGetAsync(client, "/admin/dimensions", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var items = await ReadItemsAsync(response);
        var mine = items.Where(item => IsOneOf(item, mixed, pending)).ToArray();

        Assert.Equal(2, mine.Length);

        var mixedItem = SingleItem(mine, mixed);
        Assert.Equal(
            new[] { "RETIRED", "ACTIVE", "DRAFT" },
            GetVersions(mixedItem).Select(version => version.GetProperty("status").GetString()));

        var pendingItem = SingleItem(mine, pending);
        Assert.Empty(GetVersions(pendingItem));
    }

    [Fact]
    public async Task AdminList_WhenAdministratorCheckIsUnavailable_ShouldFailClosedWith503()
    {
        // La comprobación administrativa sustituida por un lector que falla simula la
        // inaccesibilidad de PostgreSQL DURANTE la evaluación; el token se valida contra
        // la base real de la colección, de modo que el 503 proviene de la autorización.
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAdministratorAuthorizationRepository>();
            services.AddSingleton<IAdministratorAuthorizationRepository, UnreachableAdministratorRepository>();
        });
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var response = await SendGetAsync(client, "/admin/dimensions", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");
    }

    [Fact]
    public async Task AdminList_AfterAdministratorRevocation_ShouldDenyTheNextEvaluation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var authorized = await SendGetAsync(client, "/admin/dimensions", adminToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

        await RevokeAdministratorAsync(adminUserId);
        var denied = await SendGetAsync(client, "/admin/dimensions", adminToken);

        // La revocación se observa sin depender de la vigencia del token
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

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
        string email = $"dimension_http_{Guid.NewGuid():N}@yusay.local";
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

    // Siembra local por prueba con INSERT directo: sin seeds ni migraciones nuevas.
    private async Task<Guid> SeedDimensionAsync(string label, params (string Status, int Version)[] versions)
    {
        var dimensionId = Guid.NewGuid();
        var code = $"b4h_{label}_{Guid.NewGuid():N}";

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, @Code, 'Dimensión de prueba', 'Descripción de prueba.');
            """, new { DimensionId = dimensionId, Code = code }));

        foreach (var (status, version) in versions)
        {
            var versionId = Guid.NewGuid();

            // V015 prohíbe insertar o modificar anclas en versiones ACTIVE/RETIRED: se
            // siembra como DRAFT, se registran las anclas y solo después se transiciona
            // al estado final por el autómata DRAFT -> ACTIVE -> RETIRED (V015).
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.dimension_version
                    (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
                VALUES (@VersionId, @DimensionId, @Version, 'Definición histórica de prueba.', 1, 5, 1, 'DRAFT');
                """, new { VersionId = versionId, DimensionId = dimensionId, Version = version }));

            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label)
                VALUES (@VersionId, 1, 'Ancla mínima'), (@VersionId, 5, 'Ancla máxima');
                """, new { VersionId = versionId }));

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
        }

        return dimensionId;
    }

    private async Task GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
    }

    private async Task RevokeAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = userId });
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

    private static async Task<JsonElement[]> ReadItemsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
    }

    private static Guid GetDimensionId(JsonElement item) => item.GetProperty("dimensionId").GetGuid();

    private static JsonElement[] GetVersions(JsonElement item) => item.GetProperty("versions")
        .EnumerateArray()
        .Select(version => version.Clone())
        .ToArray();

    private static bool IsOneOf(JsonElement item, params Guid[] dimensionIds) =>
        dimensionIds.Contains(GetDimensionId(item));

    private static JsonElement SingleItem(JsonElement[] items, Guid dimensionId) =>
        Assert.Single(items, item => GetDimensionId(item) == dimensionId);

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

    /// <summary>
    /// Sustituto que simula la inaccesibilidad de PostgreSQL durante la comprobación
    /// administrativa: el handler debe traducir el fallo en 503 fail-closed.
    /// </summary>
    private sealed class UnreachableAdministratorRepository : IAdministratorAuthorizationRepository
    {
        public Task<bool> IsAdministratorAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Simulación de PostgreSQL inaccesible durante la comprobación administrativa.");
    }
}
