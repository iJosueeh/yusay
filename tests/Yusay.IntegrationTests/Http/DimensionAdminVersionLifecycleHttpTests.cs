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
using Npgsql;
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
public sealed class DimensionAdminVersionLifecycleHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-dimension-admin-version-lifecycle-http-test-secret-0123456789";

    private const string ValidPassword = "NormativePassword#2026";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private const string TokenRejectedMessage =
        "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private const string DimensionNotFoundMessage = "La Dimensión solicitada no existe.";

    private const string VersionNotFoundMessage = "La versión de Dimensión solicitada no existe.";

    private const string ActivateNonDraftMessage =
        "La versión de la Dimensión solo puede activarse mientras está en estado DRAFT.";

    private const string RetireNonActiveMessage =
        "La versión de la Dimensión solo puede retirarse mientras está en estado ACTIVE.";

    private const string AnotherActiveMessage =
        "La Dimensión ya tiene una versión en estado ACTIVE; debe retirarse la versión activa antes de activar otra.";

    private const string PatchNonDraftMessage =
        "La versión de la Dimensión solo puede editarse mientras está en estado DRAFT.";

    private const string CheckInWithoutActiveVersionFragment =
        "no tiene una versión de escala activa";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public DimensionAdminVersionLifecycleHttpTests(PostgreSqlFixture fixture)
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
    public async Task Transitions_WithoutOrWithInvalidToken_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var missingActivate = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation");
        await AssertProblemAsync(
            missingActivate, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var missingRetire = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement");
        await AssertProblemAsync(
            missingRetire, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        var invalidActivate = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation",
            $"invalid_{Guid.NewGuid():N}");
        await AssertProblemAsync(
            invalidActivate, HttpStatusCode.Unauthorized, "No autorizado", TokenRejectedMessage);

        var invalidRetire = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement",
            $"invalid_{Guid.NewGuid():N}");
        await AssertProblemAsync(
            invalidRetire, HttpStatusCode.Unauthorized, "No autorizado", TokenRejectedMessage);
    }

    [Fact]
    public async Task Transitions_WithAuthenticatedNonAdministrator_ShouldReturn403ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var activate = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);
        await AssertProblemAsync(
            activate, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");

        var retire = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement", accessToken);
        await AssertProblemAsync(
            retire, HttpStatusCode.Forbidden, "Acceso denegado", "habilitación administrativa");
    }

    [Fact]
    public async Task Transitions_WhenAdministratorCheckIsUnavailable_ShouldFailClosedWith503()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAdministratorAuthorizationRepository>();
            services.AddSingleton<IAdministratorAuthorizationRepository, UnreachableAdministratorRepository>();
        });
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync();
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var activate = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);
        await AssertProblemAsync(
            activate, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");

        var retire = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement", accessToken);
        await AssertProblemAsync(
            retire, HttpStatusCode.ServiceUnavailable, "Servicio no disponible", "no está disponible");
    }

    [Fact]
    public async Task Activate_DraftVersion_ShouldReturn200PreserveTheRepresentationAuditAndPublishVisibility()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var hiddenList = await SendGetAsync(client, "/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, hiddenList.StatusCode);
        Assert.DoesNotContain(await ReadItemsAsync(hiddenList), item => IsOneOf(item, dimensionId));
        var hiddenById = await SendGetAsync(client, $"/dimensions/{dimensionId}", accessToken);
        Assert.Equal(HttpStatusCode.NotFound, hiddenById.StatusCode);
        var adminBefore = SingleItem(await ReadItemsAsync(
            await SendGetAsync(client, "/admin/dimensions", accessToken)), dimensionId);
        Assert.Equal("DRAFT", Assert.Single(GetVersions(adminBefore)).GetProperty("status").GetString());

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Location"));

        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var body = document.RootElement;
            Assert.Equal(versionId, body.GetProperty("dimensionVersionId").GetGuid());
            Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
            Assert.Equal(1, body.GetProperty("version").GetInt32());
            Assert.Equal("Percepción de ánimo del día.", body.GetProperty("definition").GetString());
            Assert.Equal(1, body.GetProperty("minValue").GetInt32());
            Assert.Equal(5, body.GetProperty("maxValue").GetInt32());
            Assert.Equal(1, body.GetProperty("step").GetInt32());
            Assert.Equal([1, 3, 5], body.GetProperty("anchors").EnumerateArray()
                .Select(anchor => anchor.GetProperty("value").GetInt32()));
        }

        var audit = Assert.Single(await QueryAuditAsync(versionId, "DIMENSION_VERSION_ACTIVATED"));
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("DIMENSION_VERSION", audit.Target_type);
        Assert.Equal(versionId.ToString(), audit.Target_identifier);
        Assert.Null(audit.Metadata);

        var visibleList = await SendGetAsync(client, "/dimensions", accessToken);
        var visibleItem = SingleItem(await ReadItemsAsync(visibleList), dimensionId);
        var visibleVersion = Assert.Single(GetVersions(visibleItem));
        Assert.Equal("ACTIVE", visibleVersion.GetProperty("status").GetString());

        var visibleById = await SendGetAsync(client, $"/dimensions/{dimensionId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, visibleById.StatusCode);
        using (var document = JsonDocument.Parse(await visibleById.Content.ReadAsStringAsync()))
        {
            var version = Assert.Single(document.RootElement.GetProperty("versions").EnumerateArray());
            Assert.Equal("ACTIVE", version.GetProperty("status").GetString());
        }

        var adminAfter = SingleItem(await ReadItemsAsync(
            await SendGetAsync(client, "/admin/dimensions", accessToken)), dimensionId);
        Assert.Equal("ACTIVE", Assert.Single(GetVersions(adminAfter)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Retire_ActiveVersion_ShouldReturn200UnpublishAndAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await SeedVersionAsync(dimensionId, 1, "ACTIVE");

        var publishedList = await SendGetAsync(client, "/dimensions", accessToken);
        Assert.Contains(await ReadItemsAsync(publishedList), item => IsOneOf(item, dimensionId));
        var publishedById = await SendGetAsync(client, $"/dimensions/{dimensionId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, publishedById.StatusCode);

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Location"));

        using (var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync()))
        {
            var body = document.RootElement;
            Assert.Equal(versionId, body.GetProperty("dimensionVersionId").GetGuid());
            Assert.Equal("RETIRED", body.GetProperty("status").GetString());
            Assert.Equal("Percepción de ánimo del día.", body.GetProperty("definition").GetString());
            Assert.Equal([1, 3, 5], body.GetProperty("anchors").EnumerateArray()
                .Select(anchor => anchor.GetProperty("value").GetInt32()));
        }

        var audit = Assert.Single(await QueryAuditAsync(versionId, "DIMENSION_VERSION_RETIRED"));
        Assert.Equal("ADMINISTRATOR", audit.Actor_kind);
        Assert.Equal(adminUserId, audit.Actor_user_id);
        Assert.Equal("DIMENSION_VERSION", audit.Target_type);
        Assert.Equal(versionId.ToString(), audit.Target_identifier);
        Assert.Null(audit.Metadata);

        var unpublishedList = await SendGetAsync(client, "/dimensions", accessToken);
        Assert.DoesNotContain(await ReadItemsAsync(unpublishedList), item => IsOneOf(item, dimensionId));

        var historyById = await SendGetAsync(client, $"/dimensions/{dimensionId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, historyById.StatusCode);
        using (var document = JsonDocument.Parse(await historyById.Content.ReadAsStringAsync()))
        {
            var version = Assert.Single(document.RootElement.GetProperty("versions").EnumerateArray());
            Assert.Equal("RETIRED", version.GetProperty("status").GetString());
        }

        var adminItem = SingleItem(await ReadItemsAsync(
            await SendGetAsync(client, "/admin/dimensions", accessToken)), dimensionId);
        Assert.Equal("RETIRED", Assert.Single(GetVersions(adminItem)).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Activate_AfterRetiringThePreviousActive_ShouldRepublishWithTheNewVersion()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var firstVersionId = await SeedVersionAsync(dimensionId, 1, "ACTIVE");
        var secondVersionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId, version: 2);

        var retire = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{firstVersionId}/retirement", accessToken);
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);

        var activate = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{secondVersionId}/activation", accessToken);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        var publishedList = await SendGetAsync(client, "/dimensions", accessToken);
        var item = SingleItem(await ReadItemsAsync(publishedList), dimensionId);
        var publishedVersion = Assert.Single(GetVersions(item));
        Assert.Equal(secondVersionId, publishedVersion.GetProperty("dimensionVersionId").GetGuid());
        Assert.Equal("ACTIVE", publishedVersion.GetProperty("status").GetString());

        var historyById = await SendGetAsync(client, $"/dimensions/{dimensionId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, historyById.StatusCode);
        using (var document = JsonDocument.Parse(await historyById.Content.ReadAsStringAsync()))
        {
            Assert.Equal([1, 2], document.RootElement.GetProperty("versions").EnumerateArray()
                .Select(version => version.GetProperty("version").GetInt32()));
            Assert.Contains(document.RootElement.GetProperty("versions").EnumerateArray(),
                version => version.GetProperty("status").GetString() == "RETIRED");
            Assert.Contains(document.RootElement.GetProperty("versions").EnumerateArray(),
                version => version.GetProperty("status").GetString() == "ACTIVE");
        }

        Assert.Single(await QueryAuditAsync(firstVersionId, "DIMENSION_VERSION_RETIRED"));
        Assert.Single(await QueryAuditAsync(secondVersionId, "DIMENSION_VERSION_ACTIVATED"));
    }

    [Fact]
    public async Task Activate_ForUnknownDimensionOrVersion_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);
        var otherDimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var foreignVersionId = await CreateVersionViaHttpAsync(
            client, accessToken, otherDimensionId);

        var unknownDimension = await SendPostAsync(
            client, $"/admin/dimensions/{Guid.NewGuid()}/versions/{versionId}/activation", accessToken);
        await AssertProblemAsync(
            unknownDimension, HttpStatusCode.NotFound, "Recurso no encontrado", DimensionNotFoundMessage);

        var unknownVersion = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{Guid.NewGuid()}/activation", accessToken);
        await AssertProblemAsync(
            unknownVersion, HttpStatusCode.NotFound, "Recurso no encontrado", VersionNotFoundMessage);

        var foreignVersion = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{foreignVersionId}/activation", accessToken);
        await AssertProblemAsync(
            foreignVersion, HttpStatusCode.NotFound, "Recurso no encontrado", VersionNotFoundMessage);

        Assert.Equal("DRAFT", (await QueryVersionRowAsync(versionId))!.Status);
        Assert.Empty(await QueryAuditAsync(versionId, "DIMENSION_VERSION_ACTIVATED"));
    }

    [Fact]
    public async Task Retire_ForUnknownDimensionOrVersion_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await SeedVersionAsync(dimensionId, 1, "ACTIVE");
        var otherDimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var foreignVersionId = await SeedVersionAsync(otherDimensionId, 1, "ACTIVE");

        var unknownDimension = await SendPostAsync(
            client, $"/admin/dimensions/{Guid.NewGuid()}/versions/{versionId}/retirement", accessToken);
        await AssertProblemAsync(
            unknownDimension, HttpStatusCode.NotFound, "Recurso no encontrado", DimensionNotFoundMessage);

        var unknownVersion = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{Guid.NewGuid()}/retirement", accessToken);
        await AssertProblemAsync(
            unknownVersion, HttpStatusCode.NotFound, "Recurso no encontrado", VersionNotFoundMessage);

        var foreignVersion = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{foreignVersionId}/retirement", accessToken);
        await AssertProblemAsync(
            foreignVersion, HttpStatusCode.NotFound, "Recurso no encontrado", VersionNotFoundMessage);

        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(versionId))!.Status);
        Assert.Empty(await QueryAuditAsync(versionId, "DIMENSION_VERSION_RETIRED"));
    }

    [Theory]
    [InlineData("ACTIVE")]
    [InlineData("RETIRED")]
    public async Task Activate_ForNonDraftVersion_ShouldReturn409(string status)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await SeedVersionAsync(dimensionId, 1, status);

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Conflicto", ActivateNonDraftMessage);
        Assert.Equal(status, (await QueryVersionRowAsync(versionId))!.Status);
        Assert.Empty(await QueryAuditAsync(versionId, "DIMENSION_VERSION_ACTIVATED"));
    }

    [Theory]
    [InlineData("DRAFT")]
    [InlineData("RETIRED")]
    public async Task Retire_ForNonActiveVersion_ShouldReturn409(string status)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await SeedVersionAsync(dimensionId, 1, status);

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Conflicto", RetireNonActiveMessage);
        Assert.Equal(status, (await QueryVersionRowAsync(versionId))!.Status);
        Assert.Empty(await QueryAuditAsync(versionId, "DIMENSION_VERSION_RETIRED"));
    }

    [Fact]
    public async Task Activate_WithAnchorOutsideTheScale_ShouldReturn400AndKeepTheVersionDraft()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);
        await InsertCorruptedAnchorAsync(versionId, 7, "Fuera de escala");

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.BadRequest, "Solicitud no válida", "no pertenece a la escala");
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(versionId))!.Status);
        Assert.Empty(await QueryAuditAsync(versionId, "DIMENSION_VERSION_ACTIVATED"));
    }

    [Fact]
    public async Task Activate_WhenAnotherVersionIsActive_ShouldReturn409WithoutRetiringIt()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var activeVersionId = await SeedVersionAsync(dimensionId, 1, "ACTIVE");
        var draftVersionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId, version: 2);

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{draftVersionId}/activation", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "Conflicto", AnotherActiveMessage);
        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(activeVersionId))!.Status);
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(draftVersionId))!.Status);
        Assert.Equal(1, await CountActiveVersionsAsync(dimensionId));
        Assert.Empty(await QueryAuditAsync(draftVersionId, "DIMENSION_VERSION_ACTIVATED"));
        Assert.Empty(await QueryAuditAsync(activeVersionId, "DIMENSION_VERSION_ACTIVATED"));
    }

    [Fact]
    public async Task Activate_ConcurrentDraftVersions_ShouldYieldExactlyOneWinnerAndSingleAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var firstVersionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId, version: 1);
        var secondVersionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId, version: 2);

        var responses = await Task.WhenAll(
            SendPostAsync(client,
                $"/admin/dimensions/{dimensionId}/versions/{firstVersionId}/activation", accessToken),
            SendPostAsync(client,
                $"/admin/dimensions/{dimensionId}/versions/{secondVersionId}/activation", accessToken));

        var statuses = responses.Select(response => response.StatusCode)
            .OrderBy(status => status)
            .ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, statuses);

        var winner = responses[0].StatusCode == HttpStatusCode.OK
            ? (First: firstVersionId, Second: secondVersionId)
            : (First: secondVersionId, Second: firstVersionId);
        Assert.Equal(1, await CountActiveVersionsAsync(dimensionId));
        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(winner.First))!.Status);
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(winner.Second))!.Status);
        Assert.Single(await QueryAuditAsync(winner.First, "DIMENSION_VERSION_ACTIVATED"));
        Assert.Empty(await QueryAuditAsync(winner.Second, "DIMENSION_VERSION_ACTIVATED"));
        Assert.Empty(await QueryAuditAsync(winner.Second, "DIMENSION_VERSION_RETIRED"));
    }

    [Fact]
    public async Task Patch_ConcurrentWithActivation_ShouldSerializeAndRejectTheEditOnceActive()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);
        var patchPath = $"/admin/dimensions/{dimensionId}/versions/{versionId}";
        var activationPath = $"{patchPath}/activation";

        var responses = await Task.WhenAll(
            SendJsonAsync(client, HttpMethod.Patch, patchPath,
                Json("""{"definition":"Definición concurrente."}"""), accessToken),
            SendPostAsync(client, activationPath, accessToken));

        var patchResponse = responses[0];
        var activationResponse = responses[1];
        Assert.Equal(HttpStatusCode.OK, activationResponse.StatusCode);
        Assert.Contains(patchResponse.StatusCode,
            new[] { HttpStatusCode.OK, HttpStatusCode.Conflict });

        var row = await QueryVersionRowAsync(versionId);
        Assert.Equal("ACTIVE", row!.Status);
        Assert.Equal(
            patchResponse.StatusCode == HttpStatusCode.OK ? "Definición concurrente." : "Percepción de ánimo del día.",
            row.Definition);

        Assert.Single(await QueryAuditAsync(versionId, "DIMENSION_VERSION_ACTIVATED"));
        var edits = await QueryAuditAsync(versionId, "CATALOG_UPDATED");
        Assert.Equal(patchResponse.StatusCode == HttpStatusCode.OK ? 1 : 0, edits.Length);

        var rejectedPatch = await SendJsonAsync(client, HttpMethod.Patch, patchPath,
            Json("""{"definition":"Edición posterior."}"""), accessToken);
        await AssertProblemAsync(
            rejectedPatch, HttpStatusCode.Conflict, "Conflicto", PatchNonDraftMessage);
        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(versionId))!.Status);
    }

    [Fact]
    public async Task Activate_WhenAuditFails_ShouldReturn500AndRollBackTheTransition()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditEventRepository>();
            services.AddSingleton<IAuditEventRepository>(
                new FailingTransitionAuditEventRepository(
                    new AuditEventRepository(_fixture.ConnectionFactory)));
        });
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId);

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "Error interno del servidor",
            "Fallo inyectado de PostgreSQL al registrar la auditoría de transición");
        Assert.Equal("DRAFT", (await QueryVersionRowAsync(versionId))!.Status);
        Assert.Empty(await QueryAuditAsync(versionId, "DIMENSION_VERSION_ACTIVATED"));
        Assert.Equal(0, await CountActiveVersionsAsync(dimensionId));
    }

    [Fact]
    public async Task Retire_WhenAuditFails_ShouldReturn500AndRollBackTheTransition()
    {
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAuditEventRepository>();
            services.AddSingleton<IAuditEventRepository>(
                new FailingTransitionAuditEventRepository(
                    new AuditEventRepository(_fixture.ConnectionFactory)));
        });
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await SeedVersionAsync(dimensionId, 1, "ACTIVE");

        var response = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement", accessToken);

        await AssertProblemAsync(
            response, HttpStatusCode.InternalServerError, "Error interno del servidor",
            "Fallo inyectado de PostgreSQL al registrar la auditoría de transición");
        Assert.Equal("ACTIVE", (await QueryVersionRowAsync(versionId))!.Status);
        Assert.Empty(await QueryAuditAsync(versionId, "DIMENSION_VERSION_RETIRED"));
    }

    [Fact]
    public async Task Lifecycle_ShouldKeepHistoricalMeasurementsQueryableAfterRetirement()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var firstVersionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId, version: 1);
        var activateFirst = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{firstVersionId}/activation", accessToken);
        Assert.Equal(HttpStatusCode.OK, activateFirst.StatusCode);

        var historicalCheckInId = await CreateCheckInAsync(client, accessToken, dimensionId, value: 3);
        Assert.Equal(firstVersionId, await QueryMeasurementVersionIdAsync(historicalCheckInId, dimensionId));

        var retire = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{firstVersionId}/retirement", accessToken);
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);

        var historical = await SendGetAsync(client, $"/check-ins/{historicalCheckInId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        using (var document = JsonDocument.Parse(await historical.Content.ReadAsStringAsync()))
        {
            var measurement = Assert.Single(document.RootElement.GetProperty("measurements").EnumerateArray());
            Assert.Equal(dimensionId, measurement.GetProperty("dimensionId").GetGuid());
            Assert.Equal(firstVersionId, measurement.GetProperty("dimensionVersionId").GetGuid());
            Assert.Equal(3, measurement.GetProperty("value").GetInt32());
        }
        Assert.Equal(firstVersionId, await QueryMeasurementVersionIdAsync(historicalCheckInId, dimensionId));

        var deleteException = await Assert.ThrowsAsync<PostgresException>(async () =>
        {
            await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
            await connection.ExecuteAsync(new CommandDefinition(
                "DELETE FROM yusay.dimension_version WHERE dimension_version_id = @VersionId",
                new { VersionId = firstVersionId }));
        });
        Assert.Equal("23001", deleteException.SqlState);

        var secondVersionId = await CreateVersionScaleViaHttpAsync(
            client, accessToken, dimensionId, version: 2, minValue: 1, maxValue: 7);
        var activateSecond = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{secondVersionId}/activation", accessToken);
        Assert.Equal(HttpStatusCode.OK, activateSecond.StatusCode);

        var currentCheckInId = await CreateCheckInAsync(client, accessToken, dimensionId, value: 7);
        Assert.Equal(secondVersionId, await QueryMeasurementVersionIdAsync(currentCheckInId, dimensionId));
        Assert.Equal(firstVersionId, await QueryMeasurementVersionIdAsync(historicalCheckInId, dimensionId));
    }

    [Fact]
    public async Task Retire_WithoutRemainingActiveVersion_ShouldKeepTheExistingCheckInConflictBehavior()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var accessToken = await CreateSessionAsyncAndGrant();
        var dimensionId = await CreateDimensionViaHttpAsync(client, accessToken);
        var versionId = await CreateVersionViaHttpAsync(client, accessToken, dimensionId, version: 1);
        var activate = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);

        var beforeRetirement = await SendCheckInAsync(client, accessToken, dimensionId, value: 3);
        Assert.Equal(HttpStatusCode.Created, beforeRetirement.StatusCode);

        var retire = await SendPostAsync(
            client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/retirement", accessToken);
        Assert.Equal(HttpStatusCode.OK, retire.StatusCode);

        var afterRetirement = await SendCheckInAsync(client, accessToken, dimensionId, value: 3);
        await AssertProblemAsync(
            afterRetirement, HttpStatusCode.Conflict, "Conflicto",
            CheckInWithoutActiveVersionFragment);
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
        string email = $"b6_lifecycle_{Guid.NewGuid():N}@yusay.local";
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
                code = $"b6lc_{Guid.NewGuid():N}",
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
                {"version":%version%,"definition":"Percepción de ánimo del día.","minValue":1,"maxValue":5,"step":1,
                 "anchors":[{"value":1,"label":"Bajo"},{"value":3,"label":"Medio"},{"value":5,"label":"Alto"}]}
                """.Replace("%version%", version.ToString())),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadVersionIdAsync(response);
    }

    private async Task<Guid> CreateVersionScaleViaHttpAsync(
        HttpClient client, string accessToken, Guid dimensionId, int version, int minValue, int maxValue)
    {
        var response = await SendJsonAsync(
            client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
            Json($$"""
                {"version":{{version}},"definition":"Escala ampliada.","minValue":{{minValue}},"maxValue":{{maxValue}},"step":1,
                 "anchors":[{"value":{{minValue}},"label":"Mínimo"},{"value":{{maxValue}},"label":"Máximo"}]}
                """),
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
    }

    private async Task<Guid> SeedVersionAsync(Guid dimensionId, int version, string status)
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

        if (status is "ACTIVE" or "RETIRED")
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

    private async Task InsertCorruptedAnchorAsync(Guid dimensionVersionId, int value, string label)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "ALTER TABLE yusay.dimension_anchor DISABLE TRIGGER trg_guard_dimension_anchor_scale;");
        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO yusay.dimension_anchor (dimension_version_id, value, label)
                VALUES (@VersionId, @Value, @Label);
                """,
                new { VersionId = dimensionVersionId, Value = value, Label = label }));
        }
        finally
        {
            await connection.ExecuteAsync(
                "ALTER TABLE yusay.dimension_anchor ENABLE TRIGGER trg_guard_dimension_anchor_scale;");
        }
    }

    private async Task<Guid> CreateCheckInAsync(
        HttpClient client, string accessToken, Guid dimensionId, int value)
    {
        var response = await SendCheckInAsync(client, accessToken, dimensionId, value);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("checkInId").GetGuid();
    }

    private static Task<HttpResponseMessage> SendCheckInAsync(
        HttpClient client, string accessToken, Guid dimensionId, int value)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/check-ins")
        {
            Content = JsonContent.Create(new
            {
                measurements = new[] { new { dimensionId, value } }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
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

    private static Task<HttpResponseMessage> SendPostAsync(
        HttpClient client, string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
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
        public string Definition { get; init; } = string.Empty;
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

    private async Task<VersionRow?> QueryVersionRowAsync(Guid dimensionVersionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleOrDefaultAsync<VersionRow>(new CommandDefinition(
            """
            SELECT definition, status
            FROM yusay.dimension_version
            WHERE dimension_version_id = @DimensionVersionId
            """, new { DimensionVersionId = dimensionVersionId }));
    }

    private async Task<int> CountActiveVersionsAsync(Guid dimensionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension_version WHERE dimension_id = @DimensionId AND status = 'ACTIVE'",
            new { DimensionId = dimensionId }));
    }

    private async Task<Guid?> QueryMeasurementVersionIdAsync(Guid checkInId, Guid dimensionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            SELECT dimension_version_id
            FROM yusay.measurement
            WHERE check_in_id = @CheckInId AND dimension_id = @DimensionId
            """,
            new { CheckInId = checkInId, DimensionId = dimensionId }));
    }

    private sealed class UnreachableAdministratorRepository : IAdministratorAuthorizationRepository
    {
        public Task<bool> IsAdministratorAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Simulación de PostgreSQL inaccesible durante la comprobación administrativa.");
    }

    private sealed class FailingTransitionAuditEventRepository(IAuditEventRepository inner)
        : IAuditEventRepository
    {
        public Task AddAsync(
            AuditEvent auditEvent,
            DbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            if (auditEvent.Action is "DIMENSION_VERSION_ACTIVATED" or "DIMENSION_VERSION_RETIRED")
            {
                throw new InvalidOperationException(
                    "Fallo inyectado de PostgreSQL al registrar la auditoría de transición.");
            }

            return inner.AddAsync(auditEvent, transaction, cancellationToken);
        }
    }
}
