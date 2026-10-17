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
public sealed class DimensionAdminVersionHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-dimension-admin-version-http-integration-test-secret-0123456789";

    private const string ValidPassword = "NormativePassword#2026";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private const string TokenRejectedMessage =
        "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private const string DuplicateVersionMessage =
        "Ya existe una versión de la Dimensión con el número proporcionado.";

    private const string DimensionNotFoundMessage = "La Dimensión solicitada no existe.";

    private const string VersionNotFoundMessage = "La versión de Dimensión solicitada no existe.";

    private const string NonDraftMessage =
        "La versión de la Dimensión solo puede editarse mientras está en estado DRAFT.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public DimensionAdminVersionHttpTests(PostgreSqlFixture fixture)
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
    public async Task VersionWrites_WithoutOrWithInvalidToken_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var body = Json("""
            {"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}
            """);

        var missingCreate = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions", body);
        await AssertProblemAsync(
            missingCreate, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingPatch = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}", Json("""{"definition":"Nueva."}"""));
        await AssertProblemAsync(
            missingPatch, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var invalidCreate = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}"""),
            $"invalid_{Guid.NewGuid():N}");
        await AssertProblemAsync(
            invalidCreate, HttpStatusCode.Unauthorized, "No autorizado", TokenRejectedMessage);

        var invalidPatch = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""{"definition":"Nueva."}"""), $"invalid_{Guid.NewGuid():N}");
        await AssertProblemAsync(
            invalidPatch, HttpStatusCode.Unauthorized, "No autorizado", TokenRejectedMessage);
    }

    [Fact]
    public async Task VersionWrites_WithAuthenticatedNonAdministrator_ShouldReturn403ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();
        var dimensionId = Guid.NewGuid();

        var create = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}"""),
            accessToken);
        await AssertProblemAsync(
            create, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var patch = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{Guid.NewGuid()}",
            Json("""{"definition":"Nueva."}"""), accessToken);
        await AssertProblemAsync(
            patch, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");
    }

    [Fact]
    public async Task VersionWrites_WhenAdministratorCheckIsUnavailable_ShouldFailClosedWith503()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAdministratorAuthorizationRepository>();
            services.AddSingleton<IAdministratorAuthorizationRepository, UnreachableAdministratorRepository>();
        });
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();
        var dimensionId = Guid.NewGuid();

        var create = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}"""),
            accessToken);
        await AssertProblemAsync(
            create, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");

        var patch = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{Guid.NewGuid()}",
            Json("""{"definition":"Nueva."}"""), accessToken);
        await AssertProblemAsync(
            patch, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");
    }

    [Fact]
    public async Task Create_WithAdministrator_ShouldReturn201WithDraftRepresentationAndAuditCreated()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""
                {"version":2,"definition":"  Percepción del ánimo del día.  ",
                 "minValue":1,"maxValue":5,"step":1,
                 "anchors":[{"value":1,"label":"  Bajo  "},{"value":3,"label":"Medio"},{"value":5,"label":"Alto"}]}
                """),
            accessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(response.Headers.Contains("Location"));

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = document.RootElement;
        var versionId = body.GetProperty("dimensionVersionId").GetGuid();
        Assert.NotEqual(Guid.Empty, versionId);
        Assert.Equal(2, body.GetProperty("version").GetInt32());
        Assert.Equal("DRAFT", body.GetProperty("status").GetString());
        Assert.Equal("Percepción del ánimo del día.", body.GetProperty("definition").GetString());
        Assert.Equal(1, body.GetProperty("minValue").GetInt32());
        Assert.Equal(5, body.GetProperty("maxValue").GetInt32());
        Assert.Equal(1, body.GetProperty("step").GetInt32());
        Assert.Equal([1, 3, 5], body.GetProperty("anchors").EnumerateArray()
            .Select(anchor => anchor.GetProperty("value").GetInt32()));
        Assert.Equal("Bajo", body.GetProperty("anchors").EnumerateArray().First()
            .GetProperty("label").GetString());

        var audit = Assert.Single(await QueryAuditAsync(versionId));
        Assert.Equal("CATALOG_CREATED", audit.Action);
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("DIMENSION_VERSION", audit.Target_type);
        Assert.Equal(versionId.ToString(), audit.Target_identifier);
        Assert.Null(audit.Metadata);
    }

    [Fact]
    public async Task Create_WithoutAnchors_ShouldReturn201WithEmptyAnchorCollection()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.GetProperty("anchors").EnumerateArray());
        Assert.Null((await QueryAuditAsync(
            document.RootElement.GetProperty("dimensionVersionId").GetGuid())).Single().Metadata);
    }

    [Fact]
    public async Task Create_DraftVersionOfDraftOnlyDimension_ShouldBeVisibleOnlyInTheAdminList()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var adminResponse = await SendGetAsync(client, "/admin/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
        var adminItem = SingleItem(await ReadItemsAsync(adminResponse), dimensionId);
        var adminVersion = Assert.Single(GetVersions(adminItem));
        Assert.Equal(versionId, adminVersion.GetProperty("dimensionVersionId").GetGuid());
        Assert.Equal("DRAFT", adminVersion.GetProperty("status").GetString());

        var userResponse = await SendGetAsync(client, "/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, userResponse.StatusCode);
        Assert.DoesNotContain(await ReadItemsAsync(userResponse), item => IsOneOf(item, dimensionId));

        var byIdResponse = await SendGetAsync(
            client, $"/dimensions/{dimensionId}", accessToken);
        Assert.Equal(HttpStatusCode.NotFound, byIdResponse.StatusCode);
    }

    [Fact]
    public async Task Create_DraftVersionOfDimensionWithActiveVersion_ShouldStayHiddenInTheUserReads()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        await SeedVersionAsync(dimensionId, 1, "ACTIVE");
        var draftId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId, version: 2);

        var byIdResponse = await SendGetAsync(client, $"/dimensions/{dimensionId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, byIdResponse.StatusCode);
        using var document = JsonDocument.Parse(await byIdResponse.Content.ReadAsStringAsync());
        var version = Assert.Single(document.RootElement.GetProperty("versions").EnumerateArray());
        Assert.Equal(1, version.GetProperty("version").GetInt32());
        Assert.Equal("ACTIVE", version.GetProperty("status").GetString());

        var adminResponse = await SendGetAsync(client, "/admin/dimensions", accessToken);
        var adminItem = SingleItem(await ReadItemsAsync(adminResponse), dimensionId);
        Assert.Equal([1, 2], GetVersions(adminItem)
            .Select(item => item.GetProperty("version").GetInt32()));
        Assert.Contains(GetVersions(adminItem), item =>
            item.GetProperty("dimensionVersionId").GetGuid() == draftId &&
            item.GetProperty("status").GetString() == "DRAFT");
    }

    [Fact]
    public async Task Create_ForUnknownDimension_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();

        var response = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{Guid.NewGuid()}/versions",
            Json("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}"""),
            accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "Recurso no encontrado", DimensionNotFoundMessage);
    }

    [Fact]
    public async Task Create_WithDuplicatedVersionNumber_ShouldReturn409WithUniformMessageAndNoAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var body = Json("""
            {"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}
            """);

        var first = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions", body, accessToken);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var winnerId = await ReadVersionIdAsync(first);

        var second = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""
                {"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}
                """),
            accessToken);
        await AssertProblemAsync(
            second, HttpStatusCode.Conflict, "Conflicto", DuplicateVersionMessage);

        Assert.Equal(1, await CountVersionsAsync(dimensionId));
        Assert.Single(await QueryAuditAsync(winnerId));
    }

    [Theory]
    [InlineData("""{"version":0,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}""", "entero positivo")]
    [InlineData("""{"version":-1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}""", "entero positivo")]
    [InlineData("""{"version":1,"definition":"   ","minValue":1,"maxValue":5,"step":1}""", "definición")]
    [InlineData("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":0}""", "incremento")]
    [InlineData("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":3}""", "divisible")]
    [InlineData("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1,"anchors":[{"value":7,"label":"Muy alto"}]}""", "no pertenece a la escala")]
    [InlineData("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1,"anchors":[{"value":1,"label":"   "}]}""", "etiqueta")]
    [InlineData("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1,"anchors":[{"value":1,"label":"Bajo"},{"value":1,"label":"Otro"}]}""", "duplicados")]
    public async Task Create_WithInvalidForm_ShouldReturn400(string body, string detailFragment)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json(body), accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Solicitud no válida", detailFragment);
        Assert.Equal(0, await CountVersionsAsync(dimensionId));
    }

    [Fact]
    public async Task Create_WhenAuditFails_ShouldReturn500AndRollBackTheVersion()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditEventRepository>();
            services.AddSingleton<IAuditEventRepository>(
                new FailingCatalogAuditEventRepository(
                    new AuditEventRepository(_fixture.ConnectionFactory)));
        });
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);

        var response = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""{"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}"""),
            accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "Error interno del servidor",
            "Fallo inyectado de PostgreSQL al registrar la auditoría del catálogo");
        Assert.Equal(0, await CountVersionsAsync(dimensionId));
    }

    [Fact]
    public async Task Patch_DefinitionOnly_ShouldReturn200AndAuditDefinition()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""{"definition":"  Nueva percepción del día.  "}"""), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Nueva percepción del día.", document.RootElement.GetProperty("definition").GetString());
        Assert.Equal(5, document.RootElement.GetProperty("maxValue").GetInt32());
        Assert.Equal(3, document.RootElement.GetProperty("anchors").EnumerateArray().Count());

        var row = await QueryVersionRowAsync(versionId);
        Assert.Equal("Nueva percepción del día.", row!.Definition);
        Assert.Equal(1, row.Version);
        Assert.Equal("DRAFT", row.Status);

        Assert.Equal(["definition"], ReadChangedFields(
            Assert.Single(await QueryAuditAsync(versionId, "CATALOG_UPDATED")).Metadata));
    }

    [Fact]
    public async Task Patch_ScaleOnly_ShouldAuditJustTheScaleFieldsReallyChanged()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""{"minValue":0,"maxValue":7,"step":1}"""), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await QueryVersionRowAsync(versionId);
        Assert.Equal(0, row!.Min_value);
        Assert.Equal(7, row.Max_value);

        Assert.Equal(["min_value", "max_value"], ReadChangedFields(
            Assert.Single(await QueryAuditAsync(versionId, "CATALOG_UPDATED")).Metadata));
    }

    [Fact]
    public async Task Patch_AnchorReplacement_ShouldPersistTheDiffAndAuditNullMetadata()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""
                {"anchors":[{"value":1,"label":"Muy bajo"},{"value":2,"label":"Bajo-medio"},{"value":5,"label":"Alto"}]}
                """),
            accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            [(1, "Muy bajo"), (2, "Bajo-medio"), (5, "Alto")],
            await QueryAnchorsAsync(versionId));

        Assert.Null(Assert.Single(await QueryAuditAsync(versionId, "CATALOG_UPDATED")).Metadata);
    }

    [Fact]
    public async Task Patch_RelabelOnly_ShouldPersistTheNewLabelAndAuditNullMetadata()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""{"anchors":[{"value":1,"label":"Bajo"},{"value":3,"label":"Intermedio"},{"value":5,"label":"Alto"}]}"""),
            accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains((3, "Intermedio"), await QueryAnchorsAsync(versionId));

        Assert.Null(Assert.Single(await QueryAuditAsync(versionId, "CATALOG_UPDATED")).Metadata);
    }

    [Fact]
    public async Task Patch_EmptyAnchors_ShouldRemoveTheWholeSetAndAuditNullMetadata()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""{"anchors":[]}"""), accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(document.RootElement.GetProperty("anchors").EnumerateArray());

        Assert.Empty(await QueryAnchorsAsync(versionId));
        Assert.Null(Assert.Single(await QueryAuditAsync(versionId, "CATALOG_UPDATED")).Metadata);
    }

    [Fact]
    public async Task Patch_CombinedScaleAndAnchors_ShouldApplyTheChangeUnderTheG1G4Triggers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""
                {"minValue":1,"maxValue":3,"step":1,"anchors":[{"value":1,"label":"Bajo"},{"value":2,"label":"Medio"},{"value":3,"label":"Alto"}]}
                """),
            accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var row = await QueryVersionRowAsync(versionId);
        Assert.Equal(3, row!.Max_value);
        Assert.Equal([(1, "Bajo"), (2, "Medio"), (3, "Alto")], await QueryAnchorsAsync(versionId));

        Assert.Equal(["max_value"], ReadChangedFields(
            Assert.Single(await QueryAuditAsync(versionId, "CATALOG_UPDATED")).Metadata));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"definition":null,"minValue":null,"maxValue":null,"step":null,"anchors":null}""")]
    [InlineData("""{"definition":"  Percepción del ánimo del día.  "}""")]
    [InlineData("""{"anchors":[{"value":1,"label":"  Bajo  "},{"value":3,"label":"Medio"},{"value":5,"label":"Alto"}]}""")]
    public async Task Patch_WithoutEffectiveChanges_ShouldReturn400AndNotAudit(string rawBody)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}", Json(rawBody), accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Solicitud no válida", "no modifica ningún campo");
        Assert.Empty(await QueryAuditAsync(versionId, "CATALOG_UPDATED"));
        Assert.Equal(3, (await QueryAnchorsAsync(versionId)).Length);
    }

    [Theory]
    [InlineData("""{"minValue":1}""", "atómico")]
    [InlineData("""{"maxValue":7,"step":1}""", "atómico")]
    [InlineData("""{"step":2,"anchors":[{"value":1,"label":"Bajo"}]}""", "atómico")]
    [InlineData("""{"minValue":1,"maxValue":3,"step":1}""", "no pertenece a la escala")]
    [InlineData("""{"anchors":[{"value":1,"label":"Bajo"},{"value":1,"label":"Otro"},{"value":3,"label":"Medio"},{"value":5,"label":"Alto"}]}""", "duplicados")]
    [InlineData("""{"anchors":[{"value":1,"label":"Bajo"},{"value":3,"label":"   "},{"value":5,"label":"Alto"}]}""", "etiqueta")]
    public async Task Patch_WithInvalidForm_ShouldReturn400(string rawBody, string detailFragment)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}", Json(rawBody), accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Solicitud no válida", detailFragment);
        Assert.Empty(await QueryAuditAsync(versionId, "CATALOG_UPDATED"));
        Assert.Equal("Percepción del ánimo del día.",
            (await QueryVersionRowAsync(versionId))!.Definition);
    }

    [Fact]
    public async Task Patch_ForUnknownDimensionOrVersion_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var unknownDimension = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{Guid.NewGuid()}/versions/{versionId}",
            Json("""{"definition":"Nueva."}"""), accessToken);
        await AssertProblemAsync(
            unknownDimension, HttpStatusCode.NotFound, "Recurso no encontrado",
            DimensionNotFoundMessage);

        var unknownVersion = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{Guid.NewGuid()}",
            Json("""{"definition":"Nueva."}"""), accessToken);
        await AssertProblemAsync(
            unknownVersion, HttpStatusCode.NotFound, "Recurso no encontrado",
            VersionNotFoundMessage);
    }

    [Fact]
    public async Task Patch_ForVersionBelongingToAnotherDimension_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var firstDimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var secondDimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var foreignVersionId = await CreateVersionViaHttpAsync(
            client, accessToken, secondDimensionId);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{firstDimensionId}/versions/{foreignVersionId}",
            Json("""{"definition":"Nueva."}"""), accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.NotFound, "Recurso no encontrado", VersionNotFoundMessage);
        Assert.Equal("Percepción del ánimo del día.",
            (await QueryVersionRowAsync(foreignVersionId))!.Definition);
    }

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("RETIRED")]
    public async Task Patch_ForNonDraftVersion_ShouldReturn409(string status)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await SeedVersionAsync(dimensionId, 1, status);

        var response = await SendJsonAsync(
            client, HttpMethod.Patch,
            $"/admin/dimensions/{dimensionId}/versions/{versionId}",
            Json("""{"definition":"Nueva definición."}"""), accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Conflicto", NonDraftMessage);
        Assert.Empty(await QueryAuditAsync(versionId, "CATALOG_UPDATED"));
        Assert.Equal("Percepción de ánimo del día.",
            (await QueryVersionRowAsync(versionId))!.Definition);
    }

    [Fact]
    public async Task Create_ConcurrentWithTheSameVersionNumber_ShouldYieldOne201One409AndSingleAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var path = $"/admin/dimensions/{dimensionId}/versions";
        var body = """
            {"version":1,"definition":"Percepción del ánimo.","minValue":1,"maxValue":5,"step":1}
            """;

        var responses = await Task.WhenAll(
            SendJsonAsync(client, HttpMethod.Post, path, Json(body), accessToken),
            SendJsonAsync(client, HttpMethod.Post, path, Json(body), accessToken));

        var statuses = responses.Select(response => response.StatusCode)
            .OrderBy(status => status)
            .ToArray();
        Assert.Equal(new[] { HttpStatusCode.Created, HttpStatusCode.Conflict }, statuses);

        Assert.Equal(1, await CountVersionsAsync(dimensionId));
        var winnerId = await ReadVersionIdAsync(Assert.Single(responses,
            response => response.StatusCode == HttpStatusCode.Created));
        Assert.Single(await QueryAuditAsync(winnerId));
    }

    [Fact]
    public async Task Patch_ConcurrentOnTheSameVersion_ShouldSerializeUnderForUpdateLock()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);
        var path = $"/admin/dimensions/{dimensionId}/versions/{versionId}";

        var responses = await Task.WhenAll(
            SendJsonAsync(client, HttpMethod.Patch, path, Json("""{"definition":"Definición A."}"""), accessToken),
            SendJsonAsync(client, HttpMethod.Patch, path, Json("""{"definition":"Definición B."}"""), accessToken));

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));

        var row = await QueryVersionRowAsync(versionId);
        Assert.True(
            row!.Definition is "Definición A." or "Definición B.",
            $"La definición final ('{row.Definition}') debería ser la de una de las dos ediciones.");

        var audits = await QueryAuditAsync(versionId, "CATALOG_UPDATED");
        Assert.Equal(2, audits.Length);
        Assert.All(audits, audit => Assert.Equal(["definition"], ReadChangedFields(audit.Metadata)));
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
        string email = $"dimension_admin_version_{Guid.NewGuid():N}@yusay.local";
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

    private async Task<Guid> CreateDimensionViaHttpAsync(HttpClient client, string accessToken)
    {
        var response = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions",
            JsonContent.Create(new
            {
                code = $"b5bw_{Guid.NewGuid():N}",
                name = "Ánimo administrado",
                description = "Descripción administrada de prueba."
            }),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadDimensionIdAsync(response);
    }

    private async Task<Guid> CreateVersionViaHttpAsync(
        HttpClient client, string accessToken, Guid dimensionId, int version = 1)
    {
        var response = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json("""
                {"version":%version%,"definition":"Percepción del ánimo del día.","minValue":1,"maxValue":5,"step":1,
                 "anchors":[{"value":1,"label":"Bajo"},{"value":3,"label":"Medio"},{"value":5,"label":"Alto"}]}
                """.Replace("%version%", version.ToString())),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadVersionIdAsync(response);
    }

    private async Task<Guid> GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
        return userId;
    }    private async Task<Guid> SeedVersionAsync(Guid dimensionId, int version, string status)
    {
        var dimensionVersionId = Guid.NewGuid();
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, @Version, 'Percepción de ánimo del día.', 1, 5, 1, 'DRAFT');
            INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label)
            VALUES (@VersionId, 1, 'Bajo'), (@VersionId, 3, 'Medio'), (@VersionId, 5, 'Alto');
            """,
            new { VersionId = dimensionVersionId, DimensionId = dimensionId, Version = version }));

        if (status == "ACTIVE" || status == "RETIRED")
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE yusay.dimension_version SET status = 'ACTIVE' WHERE dimension_version_id = @VersionId",
                new { VersionId = dimensionVersionId }));
        }

        if (status == "RETIRED")
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = @VersionId",
                new { VersionId = dimensionVersionId }));
        }

        return dimensionVersionId;
    }

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

    private static async Task<Guid> ReadVersionIdAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("dimensionVersionId").GetGuid();
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

    private sealed class AuditRow
    {
        public string Action { get; init; } = string.Empty;
        public string Actor_kind { get; init; } = string.Empty;
        public Guid? Actor_user_id { get; init; }
        public string Target_type { get; init; } = string.Empty;
        public string Target_identifier { get; init; } = string.Empty;
        public string? Metadata { get; init; }
    }

    private sealed class VersionRow
    {
        public int Version { get; init; }
        public string Definition { get; init; } = string.Empty;
        public int Min_value { get; init; }
        public int Max_value { get; init; }
        public int Step { get; init; }
        public string Status { get; init; } = string.Empty;
    }

    private async Task<AuditRow[]> QueryAuditAsync(Guid targetIdentifier, string? action = null)
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
              """, new { TargetIdentifier = targetIdentifier.ToString(), Action = action }));
        return rows.ToArray();
    }

    private async Task<int> CountVersionsAsync(Guid dimensionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension_version WHERE dimension_id = @DimensionId",
            new { DimensionId = dimensionId }));
    }

    private async Task<VersionRow?> QueryVersionRowAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition(
            """
            SELECT version, definition, min_value, max_value, step, status
            FROM yusay.dimension_version
            WHERE dimension_version_id = @DimensionVersionId
            """, new { DimensionVersionId = dimensionVersionId }));
    }

    private async Task<(int Value, string Label)[]> QueryAnchorsAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var rows = await connection.QueryAsync<(int Value, string Label)>(new CommandDefinition(
            """
            SELECT value, label
            FROM yusay.dimension_anchor
            WHERE dimension_version_id = @DimensionVersionId
            ORDER BY value
            """, new { DimensionVersionId = dimensionVersionId }));
        return rows.ToArray();
    }

    private sealed class UnreachableAdministratorRepository : IAdministratorAuthorizationRepository
    {
        public Task<bool> IsAdministratorAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Simulación de PostgreSQL inaccesible durante la comprobación administrativa.");
    }

    private sealed class FailingCatalogAuditEventRepository(IAuditEventRepository inner)
        : IAuditEventRepository
    {
        public Task AddAsync(
            AuditEvent auditEvent,
            DbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            if (auditEvent.TargetType == "DIMENSION_VERSION")
            {
                throw new InvalidOperationException(
                    "Fallo inyectado de PostgreSQL al registrar la auditoría del catálogo.");
            }

            return inner.AddAsync(auditEvent, transaction, cancellationToken);
        }
    }
}
