using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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

/// <summary>
/// B8a de RF-021 — inicialización del catálogo inicial (OQ-PROD-006) exclusivamente por API:
/// creación de mood, stress, energy y sleep con su única versión 1 (1–5, step 1) y sus cinco
/// anclas literales, activación de cada versión, lecturas públicas y administrativas,
/// auditoría de cada paso (actor_kind ADMINISTRATOR y metadata NULL), reejecución controlada
/// con 409 sin duplicados ni auditoría adicional y registro de un CheckIn real que conserva
/// cada DimensionVersion exacta. El flujo probado no inserta datos de catálogo por SQL: solo
/// se siembra la fila de administrador de la fixture (OQ-ARCH-018 sigue OPEN) y se ejecutan
/// consultas de verificación sobre audit_event y measurement.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class InitialDimensionCatalogHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-initial-dimension-catalog-http-test-secret-0123456789";

    private const string ValidPassword = "NormativePassword#2026";

    private const string DuplicateCodeMessage =
        "Ya existe una Dimensión con el código proporcionado.";

    private const string DuplicateVersionMessage =
        "Ya existe una versión de la Dimensión con el número proporcionado.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public InitialDimensionCatalogHttpTests(PostgreSqlFixture fixture)
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
    public async Task Initialize_TheFourApprovedDimensionsThroughApi_ShouldActivateAuditRejectReexecutionAndRegisterACheckIn()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, accessToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminUserId);

        // Aislamiento: la base de la colección es compartida y esta prueba es la única que
        // usa los códigos aprobados, por lo que ningún estado previo puede condicionarla.
        foreach (var spec in ApprovedCatalog)
        {
            Assert.Equal(0, await CountDimensionsByCodeAsync(spec.Code));
        }

        var created = new Dictionary<string, CatalogIds>(StringComparer.Ordinal);

        // --------------------------------------------------------------------------------------
        // 1-3. Creación por API de las 4 Dimensiones, de su versión 1 con cinco anclas y de su
        // activación (DRAFT -> ACTIVE). Ningún paso escribe catálogo por SQL.
        // --------------------------------------------------------------------------------------
        foreach (var spec in ApprovedCatalog)
        {
            var dimensionResponse = await SendJsonAsync(
                client, HttpMethod.Post, "/admin/dimensions",
                JsonContent.Create(new
                {
                    code = spec.Code,
                    name = spec.Name,
                    description = spec.Description
                }),
                accessToken);
            Assert.Equal(HttpStatusCode.Created, dimensionResponse.StatusCode);

            Guid dimensionId;
            using (var document = JsonDocument.Parse(await dimensionResponse.Content.ReadAsStringAsync()))
            {
                var body = document.RootElement;
                dimensionId = body.GetProperty("dimensionId").GetGuid();
                Assert.Equal(spec.Code, body.GetProperty("code").GetString());
                Assert.Equal(spec.Name, body.GetProperty("name").GetString());
                Assert.Equal(spec.Description, body.GetProperty("description").GetString());
            }

            var versionResponse = await SendJsonAsync(
                client, HttpMethod.Post, $"/admin/dimensions/{dimensionId}/versions",
                JsonContent.Create(new
                {
                    version = 1,
                    definition = spec.Definition,
                    minValue = 1,
                    maxValue = 5,
                    step = 1,
                    anchors = spec.Anchors
                        .Select(anchor => new { value = anchor.Value, label = anchor.Label })
                        .ToArray()
                }),
                accessToken);
            Assert.Equal(HttpStatusCode.Created, versionResponse.StatusCode);

            Guid versionId;
            using (var document = JsonDocument.Parse(await versionResponse.Content.ReadAsStringAsync()))
            {
                var body = document.RootElement;
                versionId = body.GetProperty("dimensionVersionId").GetGuid();
                Assert.Equal(1, body.GetProperty("version").GetInt32());
                Assert.Equal("DRAFT", body.GetProperty("status").GetString());
                Assert.Equal(spec.Definition, body.GetProperty("definition").GetString());
                AssertScaleAndAnchors(body, spec);
            }

            var activation = await SendPostAsync(
                client, $"/admin/dimensions/{dimensionId}/versions/{versionId}/activation", accessToken);
            Assert.Equal(HttpStatusCode.OK, activation.StatusCode);
            using (var document = JsonDocument.Parse(await activation.Content.ReadAsStringAsync()))
            {
                var body = document.RootElement;
                Assert.Equal(versionId, body.GetProperty("dimensionVersionId").GetGuid());
                Assert.Equal("ACTIVE", body.GetProperty("status").GetString());
                Assert.Equal(spec.Definition, body.GetProperty("definition").GetString());
                AssertScaleAndAnchors(body, spec);
            }

            created.Add(spec.Code, new CatalogIds(dimensionId, versionId));
        }

        // --------------------------------------------------------------------------------------
        // 5. GET /dimensions, GET /dimensions/{id} y GET /admin/dimensions reflejan el catálogo.
        // --------------------------------------------------------------------------------------
        var publicList = await SendGetAsync(client, "/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
        var publicItems = await ReadItemsAsync(publicList);

        foreach (var spec in ApprovedCatalog)
        {
            var ids = created[spec.Code];
            var listItem = SingleItem(publicItems, ids.DimensionId);
            AssertApprovedDimension(listItem, spec);
            Assert.Single(GetVersions(listItem));

            var detail = await SendGetAsync(client, $"/dimensions/{ids.DimensionId}", accessToken);
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var detailItem = await ReadItemAsync(detail);
            AssertApprovedDimension(detailItem, spec);
            Assert.Equal(ids.DimensionId, detailItem.GetProperty("dimensionId").GetGuid());
        }

        var adminList = await SendGetAsync(client, "/admin/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);
        var adminItems = await ReadItemsAsync(adminList);
        foreach (var spec in ApprovedCatalog)
        {
            var ids = created[spec.Code];
            var adminItem = SingleItem(adminItems, ids.DimensionId);
            AssertApprovedDimension(adminItem, spec);
            var adminVersion = Assert.Single(GetVersions(adminItem));
            Assert.Equal("ACTIVE", adminVersion.GetProperty("status").GetString());
            Assert.Equal(ids.VersionId, adminVersion.GetProperty("dimensionVersionId").GetGuid());
        }

        // --------------------------------------------------------------------------------------
        // 6. Auditoría de cada paso: una fila por creación y dos por versión, actor_kind
        // ADMINISTRATOR, target correcto y metadata NULL (perfil N de V011).
        // --------------------------------------------------------------------------------------
        foreach (var spec in ApprovedCatalog)
        {
            var ids = created[spec.Code];

            var dimensionAudit = await QueryAuditAsync(ids.DimensionId);
            var dimensionRow = Assert.Single(dimensionAudit);
            Assert.Equal("CATALOG_CREATED", dimensionRow.Action);
            Assert.Equal("DIMENSION", dimensionRow.Target_type);
            Assert.Equal(ids.DimensionId.ToString(), dimensionRow.Target_identifier);
            AssertAdministrator(dimensionRow, adminUserId);

            var versionAudit = await QueryAuditAsync(ids.VersionId);
            Assert.Equal(
                new[] { "CATALOG_CREATED", "DIMENSION_VERSION_ACTIVATED" },
                versionAudit.Select(row => row.Action).ToArray());
            foreach (var row in versionAudit)
            {
                Assert.Equal("DIMENSION_VERSION", row.Target_type);
                Assert.Equal(ids.VersionId.ToString(), row.Target_identifier);
                AssertAdministrator(row, adminUserId);
            }
        }

        // --------------------------------------------------------------------------------------
        // 7. Reejecución controlada del procedimiento: repetir los pasos de creación devuelve
        // 409, no duplica filas y no añade auditoría (la API no es idempotente: el 409 es la
        // señal de que el paso ya se ejecutó).
        // --------------------------------------------------------------------------------------
        foreach (var spec in ApprovedCatalog)
        {
            var ids = created[spec.Code];

            var repeatedDimension = await SendJsonAsync(
                client, HttpMethod.Post, "/admin/dimensions",
                JsonContent.Create(new
                {
                    code = spec.Code,
                    name = spec.Name,
                    description = spec.Description
                }),
                accessToken);
            await AssertProblemAsync(
                repeatedDimension, HttpStatusCode.Conflict, "Conflicto", DuplicateCodeMessage);
            Assert.Equal(1, await CountDimensionsByCodeAsync(spec.Code));
            Assert.Single(await QueryAuditAsync(ids.DimensionId));

            var repeatedVersion = await SendJsonAsync(
                client, HttpMethod.Post, $"/admin/dimensions/{ids.DimensionId}/versions",
                JsonContent.Create(new
                {
                    version = 1,
                    definition = spec.Definition,
                    minValue = 1,
                    maxValue = 5,
                    step = 1,
                    anchors = spec.Anchors
                        .Select(anchor => new { value = anchor.Value, label = anchor.Label })
                        .ToArray()
                }),
                accessToken);
            await AssertProblemAsync(
                repeatedVersion, HttpStatusCode.Conflict, "Conflicto", DuplicateVersionMessage);
            Assert.Equal(1, await CountVersionsAsync(ids.DimensionId));
            Assert.Equal(
                new[] { "CATALOG_CREATED", "DIMENSION_VERSION_ACTIVATED" },
                (await QueryAuditAsync(ids.VersionId)).Select(row => row.Action).ToArray());
        }

        // --------------------------------------------------------------------------------------
        // 8. CheckIn real con las cuatro Dimensiones: contrato HTTP completo y conservación de
        // la DimensionVersion exacta en la lectura posterior y en la fila almacenada.
        // --------------------------------------------------------------------------------------
        var checkInResponse = await SendJsonAsync(
            client, HttpMethod.Post, "/check-ins",
            JsonContent.Create(new
            {
                measurements = ApprovedCatalog
                    .Select(spec => new
                    {
                        dimensionId = created[spec.Code].DimensionId,
                        value = spec.SampleValue
                    })
                    .ToArray()
            }),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, checkInResponse.StatusCode);

        Guid checkInId;
        using (var document = JsonDocument.Parse(await checkInResponse.Content.ReadAsStringAsync()))
        {
            checkInId = document.RootElement.GetProperty("checkInId").GetGuid();
            Assert.Equal(1, document.RootElement.GetProperty("revision").GetInt32());
        }

        var checkInRead = await SendGetAsync(client, $"/check-ins/{checkInId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, checkInRead.StatusCode);
        using (var document = JsonDocument.Parse(await checkInRead.Content.ReadAsStringAsync()))
        {
            var body = document.RootElement;
            Assert.Equal(checkInId, body.GetProperty("checkInId").GetGuid());
            Assert.Equal(1, body.GetProperty("revision").GetInt32());
            Assert.Equal(JsonValueKind.Null, body.GetProperty("updatedAt").ValueKind);

            var measurements = body.GetProperty("measurements").EnumerateArray().ToArray();
            Assert.Equal(ApprovedCatalog.Length, measurements.Length);
            foreach (var spec in ApprovedCatalog)
            {
                var ids = created[spec.Code];
                var measurement = Assert.Single(measurements, item =>
                    item.GetProperty("dimensionId").GetGuid() == ids.DimensionId);
                Assert.Equal(ids.VersionId, measurement.GetProperty("dimensionVersionId").GetGuid());
                Assert.Equal(spec.SampleValue, measurement.GetProperty("value").GetInt32());
            }
        }

        var storedMeasurements = await QueryMeasurementVersionsAsync(checkInId);
        Assert.Equal(ApprovedCatalog.Length, storedMeasurements.Count);
        foreach (var spec in ApprovedCatalog)
        {
            var ids = created[spec.Code];
            Assert.Equal(ids.VersionId, storedMeasurements[ids.DimensionId]);
        }
    }

    // ------------------------------------------------------------------------------------------
    // Contenido aprobado (OQ-PROD-006 / composición D2): códigos, descripciones, escala,
    // anclas literales y definiciones; sin contenido añadido.
    // ------------------------------------------------------------------------------------------

    private static readonly InitialDimensionSpec[] ApprovedCatalog =
    {
        new(
            "mood",
            "Mood",
            "estado de ánimo percibido",
            "Percepción medida: estado de ánimo percibido. Referencia temporal: la percepción en " +
            "el instante del registro del CheckIn, sin ventana adicional. Escala ordinal y " +
            "subjetiva de 1 a 5, no clínica. Orientación de los valores: 1 «Muy negativo» a " +
            "5 «Muy positivo», con valor más alto para un ánimo más positivo. Alcance no clínico: " +
            "no constituye un diagnóstico ni una evaluación clínica.",
            new[] { (1, "Muy negativo"), (2, "Negativo"), (3, "Ni negativo ni positivo"),
                    (4, "Positivo"), (5, "Muy positivo") },
            SampleValue: 4),
        new(
            "stress",
            "Stress",
            "estrés percibido",
            "Percepción medida: estrés percibido. Referencia temporal: la percepción en el " +
            "instante del registro del CheckIn, sin ventana adicional. Escala ordinal y subjetiva " +
            "de 1 a 5, no clínica. Orientación de los valores: 1 «Nada de estrés» a " +
            "5 «Estrés muy intenso», con valor más alto para una intensidad mayor. Alcance no " +
            "clínico: no constituye un diagnóstico ni una evaluación clínica.",
            new[] { (1, "Nada de estrés"), (2, "Poco estrés"), (3, "Estrés moderado"),
                    (4, "Mucho estrés"), (5, "Estrés muy intenso") },
            SampleValue: 2),
        new(
            "energy",
            "Energy",
            "energía percibida",
            "Percepción medida: energía percibida. Referencia temporal: la percepción en el " +
            "instante del registro del CheckIn, sin ventana adicional. Escala ordinal y subjetiva " +
            "de 1 a 5, no clínica. Orientación de los valores: 1 «Muy baja» a 5 «Muy alta», con " +
            "valor más alto para una energía mayor. Alcance no clínico: no constituye un " +
            "diagnóstico ni una evaluación clínica.",
            new[] { (1, "Muy baja"), (2, "Baja"), (3, "Moderada"), (4, "Alta"), (5, "Muy alta") },
            SampleValue: 3),
        new(
            "sleep",
            "Sleep",
            "calidad del último periodo de sueño percibida, no duración",
            "Percepción medida: calidad del último periodo de sueño percibida, no duración. " +
            "Referencia temporal: el último periodo de sueño previo al registro del CheckIn. " +
            "Escala ordinal y subjetiva de 1 a 5, no clínica. Orientación de los valores: " +
            "1 «Muy mala» a 5 «Muy buena», con valor más alto para una mejor calidad percibida. " +
            "Alcance no clínico: no constituye un diagnóstico ni una evaluación clínica.",
            new[] { (1, "Muy mala"), (2, "Mala"), (3, "Regular"), (4, "Buena"), (5, "Muy buena") },
            SampleValue: 5)
    };

    private sealed record InitialDimensionSpec(
        string Code,
        string Name,
        string Description,
        string Definition,
        (int Value, string Label)[] Anchors,
        int SampleValue);

    private sealed record CatalogIds(Guid DimensionId, Guid VersionId);

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
        string email = $"b8a_catalog_{Guid.NewGuid():N}@yusay.local";
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

    private async Task<Guid> GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
        return userId;
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

    private static async Task<JsonElement[]> ReadItemsAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
    }

    // GET /dimensions/{id} devuelve un objeto DimensionResponse individual, no una colección.
    private static async Task<JsonElement> ReadItemAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.Clone();
    }

    private static Guid GetDimensionId(JsonElement item) => item.GetProperty("dimensionId").GetGuid();

    private static JsonElement[] GetVersions(JsonElement item) => item.GetProperty("versions")
        .EnumerateArray()
        .Select(version => version.Clone())
        .ToArray();

    private static JsonElement SingleItem(JsonElement[] items, Guid dimensionId) =>
        Assert.Single(items, item => GetDimensionId(item) == dimensionId);

    private static void AssertApprovedDimension(JsonElement item, InitialDimensionSpec spec)
    {
        Assert.Equal(spec.Code, item.GetProperty("code").GetString());
        Assert.Equal(spec.Name, item.GetProperty("name").GetString());
        Assert.Equal(spec.Description, item.GetProperty("description").GetString());

        var version = Assert.Single(GetVersions(item));
        Assert.Equal(1, version.GetProperty("version").GetInt32());
        Assert.Equal("ACTIVE", version.GetProperty("status").GetString());
        Assert.Equal(spec.Definition, version.GetProperty("definition").GetString());
        AssertScaleAndAnchors(version, spec);
    }

    private static void AssertScaleAndAnchors(JsonElement version, InitialDimensionSpec spec)
    {
        Assert.Equal(1, version.GetProperty("minValue").GetInt32());
        Assert.Equal(5, version.GetProperty("maxValue").GetInt32());
        Assert.Equal(1, version.GetProperty("step").GetInt32());
        Assert.Equal(
            spec.Anchors.Select(anchor => anchor.Value).ToArray(),
            version.GetProperty("anchors").EnumerateArray()
                .Select(anchor => anchor.GetProperty("value").GetInt32()).ToArray());
        Assert.Equal(
            spec.Anchors.Select(anchor => anchor.Label).ToArray(),
            version.GetProperty("anchors").EnumerateArray()
                .Select(anchor => anchor.GetProperty("label").GetString()).ToArray());
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

    // ------------------------------------------------------------------------------------------
    // Consultas directas de verificación (sin escritura de catálogo): audit_event, conteos y
    // mediciones almacenadas, filtrados por los identificadores creados en esta prueba.
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

    private static void AssertAdministrator(AuditRow row, Guid adminUserId)
    {
        Assert.Equal("ADMINISTRATOR", row.Actor_kind);
        Assert.Equal(adminUserId, row.Actor_user_id);
        Assert.Null(row.Metadata);
    }

    private async Task<AuditRow[]> QueryAuditAsync(Guid targetIdentifier)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var rows = await connection.QueryAsync<AuditRow>(new CommandDefinition(
            """
            SELECT action, actor_kind, actor_user_id, target_type, target_identifier,
                   metadata::text AS metadata
            FROM yusay.audit_event
            WHERE target_identifier = @TargetIdentifier
            ORDER BY occurred_at, audit_event_id
            """, new { TargetIdentifier = targetIdentifier.ToString() }));
        return rows.ToArray();
    }

    private async Task<int> CountDimensionsByCodeAsync(string code)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension WHERE code = @Code", new { Code = code }));
    }

    private async Task<int> CountVersionsAsync(Guid dimensionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM yusay.dimension_version WHERE dimension_id = @DimensionId",
            new { DimensionId = dimensionId }));
    }

    private sealed class MeasurementRow
    {
        public Guid Dimension_id { get; init; }
        public Guid Dimension_version_id { get; init; }
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> QueryMeasurementVersionsAsync(Guid checkInId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var rows = await connection.QueryAsync<MeasurementRow>(new CommandDefinition(
            """
            SELECT dimension_id, dimension_version_id
            FROM yusay.measurement
            WHERE check_in_id = @CheckInId
            """,
            new { CheckInId = checkInId }));
        return rows.ToDictionary(row => row.Dimension_id, row => row.Dimension_version_id);
    }
}
