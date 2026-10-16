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
using Yusay.Domain.Tracking.Entities;
using Yusay.Domain.Tracking.ValueObjects;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.Infrastructure.Tracking.Repositories;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Pruebas HTTP de la escritura administrativa de Dimensions (B5a de RF-021): POST201 con
/// un único CATALOG_CREATED (actor ADMINISTRATOR, target DIMENSION, metadata NULL
/// — perfil N de V011), PATCH 200 con changed_fields limitado a los cambios reales (perfil C,
/// lista cerrada name/description), 401 sin token o con token inválido, 403 sin habilitación
/// administrativa (OQ-ARCH-017), 503 fail-closed ante comprobación administrativa
/// inaccesible, 400 por validación de dominio y por ausencia de cambios efectivos (sin
/// escritura ni auditoría), 404 uniforme con B4, 409 con mensaje único por código duplicado
/// (pre-check y carrera de inserción), rollback del alta cuando la auditoría falla dentro de
/// la transacción, carrera de creación con el mismo código (exactamente un 201 y un 409) y
/// serialización de PATCH concurrente bajo SELECT FOR UPDATE (última escritura gana con dos
/// auditorías). code permanece inmutable en toda edición y una Dimensión creada sin versiones
/// aparece únicamente en GET /admin/dimensions, no en GET /dimensions. Las aserciones se
/// filtran por códigos únicos porque la base de la colección es compartida.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class DimensionAdminWriteHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-dimension-admin-write-http-integration-test-secret-0123456789";

    private const string ValidPassword = "NormativePassword#2026";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private const string TokenRejectedMessage =
        "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private const string DuplicateCodeMessage =
        "Ya existe una Dimensión con el código proporcionado.";

    private const string NotFoundMessage = "La Dimensión solicitada no existe.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public DimensionAdminWriteHttpTests(PostgreSqlFixture fixture)
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
    // Autorización: 401 sin/con token inválido y 403 sin habilitación administrativa
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithoutOrWithInvalidToken_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var missing = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(NewCode("auth")));
        await AssertProblemAsync(missing, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var invalid = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(NewCode("auth")),
            $"invalid_{Guid.NewGuid():N}");
        await AssertProblemAsync(invalid, HttpStatusCode.Unauthorized, "No autorizado", TokenRejectedMessage);
    }

    [Fact]
    public async Task Patch_WithoutOrWithInvalidToken_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var path = $"/admin/dimensions/{Guid.NewGuid()}";

        var missing = await SendJsonAsync(client, HttpMethod.Patch, path, Json("""{"name":"Nuevo"}"""));
        await AssertProblemAsync(missing, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var invalid = await SendJsonAsync(
            client, HttpMethod.Patch, path, Json("""{"name":"Nuevo"}"""), $"invalid_{Guid.NewGuid():N}");
        await AssertProblemAsync(invalid, HttpStatusCode.Unauthorized, "No autorizado", TokenRejectedMessage);
    }

    [Fact]
    public async Task Create_WithAuthenticatedNonAdministrator_ShouldReturn403ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var response = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(NewCode("denied")), accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");
    }

    [Fact]
    public async Task Patch_WithAuthenticatedNonAdministrator_ShouldReturn403ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var response = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{Guid.NewGuid()}",
            Json("""{"name":"Nuevo"}"""), accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");
    }

    [Fact]
    public async Task Writes_WhenAdministratorCheckIsUnavailable_ShouldFailClosedWith503()
    {
        // El lector administrativo sustituido por uno que falla simula la inaccesibilidad de
        // PostgreSQL DURANTE la evaluación; el token se valida contra la base real, de modo
        // que el 503 proviene de la autorización en ambas escrituras.
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAdministratorAuthorizationRepository>();
            services.AddSingleton<IAdministratorAuthorizationRepository, UnreachableAdministratorRepository>();
        });
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();

        var created = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(NewCode("unreach")), accessToken);
        await AssertProblemAsync(created, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");

        var patched = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{Guid.NewGuid()}",
            Json("""{"name":"Nuevo"}"""), accessToken);
        await AssertProblemAsync(patched, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");
    }

    // ------------------------------------------------------------------------------------------
    // POST /admin/dimensions: 201, auditoría CATALOG_CREATED (perfil N) y visibilidad
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithAdministrator_ShouldReturn201WithDetailsAndAuditCatalogCreated()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var code = NewCode("created");
        var response = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions",
            CreateBody(code, "  Ánimo  ", "  Estado subjetivo del día.  "), accessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        var dimensionId = body.GetProperty("dimensionId").GetGuid();
        Assert.NotEqual(Guid.Empty, dimensionId);
        Assert.Equal(code, body.GetProperty("code").GetString());
        // El dominio normaliza con trim antes de persistir
        Assert.Equal("Ánimo", body.GetProperty("name").GetString());
        Assert.Equal("Estado subjetivo del día.", body.GetProperty("description").GetString());
        // Representación fina: sin colección ficticia de versiones
        Assert.False(body.TryGetProperty("versions", out _));

        var audit = Assert.Single(await QueryAuditAsync(dimensionId));
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("DIMENSION", audit.Target_type);
        Assert.Equal(dimensionId.ToString(), audit.Target_identifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task Create_WithoutVersions_ShouldAppearOnlyInAdminListNotInUserList()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var code = NewCode("noversion");
        var created = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(code), accessToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var dimensionId = await ReadDimensionIdAsync(created);

        // En la lectura administrativa: presente y SIN versiones (nada ficticio)
        var adminResponse = await SendGetAsync(client, "/admin/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var adminItem = SingleItem(await ReadItemsAsync(adminResponse), dimensionId);
        Assert.Empty(GetVersions(adminItem));
        Assert.Equal(code, adminItem.GetProperty("code").GetString());

        // En la lectura de usuario: ausente, porque no hay ninguna versión ACTIVE
        var userResponse = await SendGetAsync(client, "/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        Assert.DoesNotContain(await ReadItemsAsync(userResponse), item => IsOneOf(item, dimensionId));
    }

    // ------------------------------------------------------------------------------------------
    // POST /admin/dimensions: 409 por código duplicado con mensaje único y 400 de validación
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_WithDuplicatedCode_ShouldReturn409WithUniformMessageAndNoAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var code = NewCode("dupe");
        var seededId = await SeedDimensionViaRepositoryAsync(code);

        var first = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(code), accessToken);
        await AssertProblemAsync(first, HttpStatusCode.Conflict, "Conflicto", DuplicateCodeMessage);

        // La repetición vuelve a resolver por el mismo camino (pre-check): detalle idéntico
        var second = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(code), accessToken);
        await AssertProblemAsync(second, HttpStatusCode.Conflict, "Conflicto", DuplicateCodeMessage);

        // Solo la dimensión preexistente conserva el code y ninguna auditoría se registra:
        // la semilla se insertó directamente y los rechazos no llegan a insertar ni a auditar
        Assert.Equal(1, await CountDimensionsByCodeAsync(code));
        Assert.Empty(await QueryAuditAsync(seededId));
    }

    [Fact]
    public async Task Create_WithInvalidCodeOrEmptyName_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var invalidCode = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions",
            CreateBody("código inválido", "Ánimo", "Descripción."), accessToken);
        await AssertProblemAsync(invalidCode, HttpStatusCode.BadRequest, "Solicitud no válida", "no es válido");

        var emptyName = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions",
            CreateBody(NewCode("emptyname"), "   ", "Descripción."), accessToken);
        await AssertProblemAsync(
            emptyName, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El nombre de la dimensión no puede estar vacío");

        Assert.Equal(0, await CountDimensionsByCodeAsync("código inválido"));
    }

    // ------------------------------------------------------------------------------------------
    // POST /admin/dimensions: rollback del alta cuando la auditoría falla en la transacción
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_WhenAuditFails_ShouldReturn500AndRollBackTheDimension()
    {
        // El repositorio de auditoría sustituido solo falla en los eventos CATALOG_*: el alta
        // y la auditoría comparten transacción, por lo que el fallo debe revertir el INSERT.
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditEventRepository>();
            services.AddSingleton<IAuditEventRepository>(
                new FailingCatalogAuditEventRepository(
                    new AuditEventRepository(_fixture.ConnectionFactory)));
        });
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        var code = NewCode("rollback");
        var response = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions", CreateBody(code), accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "Error interno del servidor",
            "Fallo inyectado de PostgreSQL al registrar la auditoría del catálogo");
        Assert.Equal(0, await CountDimensionsByCodeAsync(code));
    }

    // ------------------------------------------------------------------------------------------
    // PATCH /admin/dimensions/{dimensionId}: 200 con changed_fields según cambios reales
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Patch_NameOnly_ShouldReturn200PreserveCodeAndAuditName()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);
        var (dimensionId, seededCode) = await CreateViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{dimensionId}",
            Json("""{"name":"Estado de ánimo"}"""), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        Assert.Equal(dimensionId, body.GetProperty("dimensionId").GetGuid());
        Assert.Equal("Estado de ánimo", body.GetProperty("name").GetString());
        // El campo description ausente conserva su valor almacenado
        Assert.Equal("Descripción administrada de prueba.", body.GetProperty("description").GetString());

        // El code es inmutable: la respuesta refleja exactamente el code preexistente
        var row = await QueryDimensionRowAsync(dimensionId);
        Assert.Equal(seededCode, row!.Code);
        Assert.Equal(seededCode, body.GetProperty("code").GetString());

        var audit = Assert.Single(await QueryAuditAsync(dimensionId, "CATALOG_UPDATED"));
        Assert.Equal("CATALOG_UPDATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("DIMENSION", audit.Target_type);
        // El comparador es semántico: jsonb canoniza el espaciado al persistir
        Assert.Equal(new[] { "name" }, ReadChangedFields(audit.Metadata));
    }

    [Fact]
    public async Task Patch_DescriptionOnly_ShouldAuditDescriptionOnly()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var (dimensionId, _) = await CreateViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{dimensionId}",
            Json("""{"description":"Nuevo criterio operativo."}"""), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await QueryDimensionRowAsync(dimensionId);
        Assert.Equal("Ánimo administrado", row!.Name);
        Assert.Equal("Nuevo criterio operativo.", row.Description);

        Assert.Equal(new[] { "description" }, ReadChangedFields(
            Assert.Single(await QueryAuditAsync(dimensionId, "CATALOG_UPDATED")).Metadata));
    }

    [Fact]
    public async Task Patch_BothFields_ShouldAuditBothChangedFields()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var (dimensionId, _) = await CreateViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{dimensionId}",
            Json("""{"name":"Ánimo general","description":"Nueva descripción."}"""), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new[] { "name", "description" }, ReadChangedFields(
            Assert.Single(await QueryAuditAsync(dimensionId, "CATALOG_UPDATED")).Metadata));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"name":null,"description":null}""")]
    [InlineData("""{"name":"  Ánimo administrado  ","description":"  Descripción administrada de prueba.  "}""")]
    public async Task Patch_WithoutEffectiveChanges_ShouldReturn400AndNotAudit(string rawBody)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var (dimensionId, _) = await CreateViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{dimensionId}", Json(rawBody), accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Solicitud no válida", "no modifica ningún campo");
        Assert.Empty(await QueryAuditAsync(dimensionId, "CATALOG_UPDATED"));

        // Sin escritura: el nombre almacenado sigue intacto
        Assert.Equal("Ánimo administrado", (await QueryDimensionRowAsync(dimensionId))!.Name);
    }

    [Fact]
    public async Task Patch_WithWhitespaceName_ShouldReturn400WithoutAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var (dimensionId, _) = await CreateViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{dimensionId}",
            Json("""{"name":"   "}"""), accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El nombre de la dimensión no puede estar vacío");
        Assert.Empty(await QueryAuditAsync(dimensionId, "CATALOG_UPDATED"));
    }

    [Fact]
    public async Task Patch_ForUnknownDimension_ShouldReturn404WithUniformB4Message()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var response = await SendJsonAsync(
            client, HttpMethod.Patch, $"/admin/dimensions/{Guid.NewGuid()}",
            Json("""{"name":"Nuevo"}"""), accessToken);

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "Recurso no encontrado", NotFoundMessage);
    }

    // ------------------------------------------------------------------------------------------
    // Concurrencia: carrera de creación con el mismo código y PATCH serializado bajo FOR UPDATE
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_ConcurrentWithSameCode_ShouldYieldOne201One409AndSingleAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var code = NewCode("race");

        // Ambas soliciciones pasan el pre-check antes de que gane una sola inserción: la
        // perdedora resuelve 23505 (uq_dimension_code) con el mismo mensaje del pre-check.
        var responses = await Task.WhenAll(
            SendJsonAsync(client, HttpMethod.Post, "/admin/dimensions",
                CreateBody(code, "Ánimo", "Descripción de la carrera."), accessToken),
            SendJsonAsync(client, HttpMethod.Post, "/admin/dimensions",
                CreateBody(code, "Ánimo", "Descripción de la carrera."), accessToken));

        var statuses = responses.Select(response => response.StatusCode)
            .OrderBy(status => status)
            .ToArray();
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, statuses);

        Assert.Equal(1, await CountDimensionsByCodeAsync(code));
        var winner = await ReadDimensionIdAsync(Assert.Single(responses,
            response => response.StatusCode == HttpStatusCode.Created));
        Assert.Single(await QueryAuditAsync(winner));
    }

    [Fact]
    public async Task Patch_ConcurrentOnSameDimension_ShouldSerializeUnderForUpdateLock()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var (dimensionId, seededCode) = await CreateViaHttpAsync(client, accessToken);
        var path = $"/admin/dimensions/{dimensionId}";

        // El FOR UPDATE serializa ambas ediciones sobre la misma fila: las dos se aplican en
        // algún orden (última escritura gana) y cada una registra su propia auditoría.
        var responses = await Task.WhenAll(
            SendJsonAsync(client, HttpMethod.Patch, path, Json("""{"name":"Nombre A"}"""), accessToken),
            SendJsonAsync(client, HttpMethod.Patch, path, Json("""{"name":"Nombre B"}"""), accessToken));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var row = await QueryDimensionRowAsync(dimensionId);
        Assert.True(
            row!.Name is "Nombre A" or "Nombre B",
            $"El nombre final ('{row.Name}') debería ser el de una de las dos ediciones.");
        // El code no participa en la edición concurrente: sigue intacto
        Assert.Equal(seededCode, row.Code);

        var audits = await QueryAuditAsync(dimensionId, "CATALOG_UPDATED");
        Assert.Equal(2, audits.Length);
        Assert.All(audits, audit =>
        {
            Assert.Equal("CATALOG_UPDATED", audit.Action);
            Assert.Equal(new[] { "name" }, ReadChangedFields(audit.Metadata));
        });
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
        string email = $"dimension_admin_write_{Guid.NewGuid():N}@yusay.local";
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

    private async Task<(Guid DimensionId, string Code)> CreateViaHttpAsync(
        HttpClient client, string accessToken)
    {
        var code = NewCode("seed");
        var response = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions",
            CreateBody(code, "Ánimo administrado", "Descripción administrada de prueba."),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadDimensionIdAsync(response), code);
    }

    private async Task GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
    }

    // Siembra directa por prueba (sin pasar por HTTP) para el escenario de duplicado previo.
    private async Task<Guid> SeedDimensionViaRepositoryAsync(string code)
    {
        var repository = new DimensionAdministrationRepository(_fixture.ConnectionFactory);
        var dimension = Dimension.Create(
            DimensionCode.Create(code), "Dimensión previa", "Sembrada directamente.");
        await repository.AddAsync(dimension);
        return dimension.DimensionId;
    }

    private static string NewCode(string label) => $"b5aw_{label}_{Guid.NewGuid():N}";

    private static HttpContent CreateBody(
        string code,
        string name = "Ánimo administrado",
        string description = "Descripción administrada de prueba.") =>
        JsonContent.Create(new { code, name, description });

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

    private static async Task<Guid> ReadDimensionIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("dimensionId").GetGuid();
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

    /// <summary>
    /// Lee changed_fields del metadata persistido. La comparación es semántica porque
    /// PostgreSQL jsonb canoniza el espaciado al escribir (p. ej. un espacio tras los dos
    /// puntos), por lo que la igualdad textual contra la salida del dominio no es estable.
    /// </summary>
    private static string[] ReadChangedFields(string? metadata)
    {
        Assert.False(string.IsNullOrWhiteSpace(metadata));
        using var document = JsonDocument.Parse(metadata!);
        return document.RootElement.GetProperty("changed_fields")
            .EnumerateArray()
            .Select(value => value.GetString()!)
            .ToArray();
    }

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

    // ------------------------------------------------------------------------------------------
    // Consultas directas: las aserciones se filtran por IDs/códigos únicos (base compartida)
    // ------------------------------------------------------------------------------------------

    private sealed class AuditRow
    {
        public string Action { get; init; } = string.Empty;
        public string Actor_kind { get; init; } = string.Empty;
        public Guid? Actor_user_id { get; init; }
        public string Target_type { get; init; } = string.Empty;
        public string Target_identifier { get; init; } = string.Empty;
        public string? Metadata { get; init; }
    }

    private sealed record DimensionRow
    {
        public Guid Dimension_id { get; init; }
        public string Code { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
    }

    private async Task<AuditRow[]> QueryAuditAsync(Guid dimensionId, string? action = null)
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
              """, new { TargetIdentifier = dimensionId.ToString(), Action = action }));
        return rows.ToArray();
    }

    private async Task<int> CountDimensionsByCodeAsync(string code)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension WHERE code = @Code", new { Code = code }));
    }

    private async Task<DimensionRow?> QueryDimensionRowAsync(Guid dimensionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<DimensionRow>(new CommandDefinition("""
            SELECT dimension_id, code, name, description
            FROM yusay.dimension
            WHERE dimension_id = @DimensionId
            """, new { DimensionId = dimensionId }));
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

    /// <summary>
    /// Repositorio de auditoría que inyecta un fallo de persistencia únicamente en los
    /// eventos CATALOG_* (el camino de B5a): el alta debe revertirse por compartir transacción.
    /// </summary>
    private sealed class FailingCatalogAuditEventRepository(IAuditEventRepository inner)
        : IAuditEventRepository
    {
        public Task AddAsync(
            AuditEvent auditEvent,
            DbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            if (auditEvent.Action.StartsWith("CATALOG_", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Fallo inyectado de PostgreSQL al registrar la auditoría del catálogo.");
            }

            return inner.AddAsync(auditEvent, transaction, cancellationToken);
        }
    }
}
