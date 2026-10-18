using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
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
public sealed class AssessmentHistoryHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-assessment-history-http-integration-test-secret-0123456789";

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

    public AssessmentHistoryHttpTests(PostgreSqlFixture fixture)
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
    public async Task List_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resp = await SendGetAsync(client, "/assessments", null);
        await AssertProblemAsync(resp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task List_WithGarbageToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resp = await SendGetAsync(client, "/assessments", "garbage-token-12345");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task List_WithOutOfRangeLimit_ShouldReturn400ProblemDetails(int limit)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resp = await SendGetAsync(client, $"/assessments?limit={limit}", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El parámetro limit debe ser un entero entre 1 y 100.");
    }

    [Fact]
    public async Task List_WithInvalidCursor_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resp = await SendGetAsync(client, "/assessments?cursor=bad-cursor-not-base64url", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El cursor proporcionado no es válido.");
    }

    [Fact]
    public async Task List_WhenEmpty_ShouldReturn200WithEmptyItemsAndNullNextCursor()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resp = await SendGetAsync(client, "/assessments", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(0, items.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task List_WithKeysetPagination_ShouldTraversePagesCorrectly()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (userId, userToken) = await CreateSessionAsync();

        var (inst1, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_pg1");
        var (inst2, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_pg2");
        var (inst3, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_pg3");

        // Crear 3 intentos para el usuario
        var startResp1 = await SendPostAsync(client, $"/instruments/{inst1}/attempts", userToken);
        startResp1.EnsureSuccessStatusCode();
        using var doc1 = JsonDocument.Parse(await startResp1.Content.ReadAsStringAsync());
        var att1 = doc1.RootElement.GetProperty("attemptId").GetGuid();

        var startResp2 = await SendPostAsync(client, $"/instruments/{inst2}/attempts", userToken);
        startResp2.EnsureSuccessStatusCode();
        using var doc2 = JsonDocument.Parse(await startResp2.Content.ReadAsStringAsync());
        var att2 = doc2.RootElement.GetProperty("attemptId").GetGuid();

        var startResp3 = await SendPostAsync(client, $"/instruments/{inst3}/attempts", userToken);
        startResp3.EnsureSuccessStatusCode();
        using var doc3 = JsonDocument.Parse(await startResp3.Content.ReadAsStringAsync());
        var att3 = doc3.RootElement.GetProperty("attemptId").GetGuid();

        // Página 1: limit = 2
        var p1Resp = await SendGetAsync(client, "/assessments?limit=2", userToken);
        Assert.Equal(HttpStatusCode.OK, p1Resp.StatusCode);
        using var p1Doc = JsonDocument.Parse(await p1Resp.Content.ReadAsStringAsync());
        var p1Items = p1Doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(2, p1Items.Count);

        var nextCursor = p1Doc.RootElement.GetProperty("nextCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(nextCursor));

        // Página 2: limit = 2 con cursor
        var p2Resp = await SendGetAsync(client, $"/assessments?limit=2&cursor={nextCursor}", userToken);
        Assert.Equal(HttpStatusCode.OK, p2Resp.StatusCode);
        using var p2Doc = JsonDocument.Parse(await p2Resp.Content.ReadAsStringAsync());
        var p2Items = p2Doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        Assert.Single(p2Items);

        Assert.Equal(JsonValueKind.Null, p2Doc.RootElement.GetProperty("nextCursor").ValueKind);

        // Los 3 intentos recuperados corresponden a att1, att2, att3
        var allIds = p1Items.Concat(p2Items).Select(x => x.GetProperty("attemptId").GetGuid()).ToList();
        Assert.Contains(att1, allIds);
        Assert.Contains(att2, allIds);
        Assert.Contains(att3, allIds);
    }

    [Fact]
    public async Task List_WhenAttemptInProgressAndExpired_ShouldShowExpiredWithoutModifyingDatabase()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (userId, userToken) = await CreateSessionAsync();
        var (instId, verId) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_exp_ro");

        // Crear intento en BD que venció hace 10 días
        var attemptId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow.AddDays(-40);
        var expiresAt = startedAt.AddHours(720); // Venció hace 10 días

        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync("""
                INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, expires_at)
                VALUES (@AttemptId, @UserId, @InstrumentId, @VersionId, 'IN_PROGRESS', @StartedAt, @ExpiresAt);
                """,
                new
                {
                    AttemptId = attemptId,
                    UserId = userId,
                    InstrumentId = instId,
                    VersionId = verId,
                    StartedAt = startedAt,
                    ExpiresAt = expiresAt
                });
        }

        // Consultar listado
        var listResp = await SendGetAsync(client, "/assessments", userToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        using var doc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items").EnumerateArray().ToList();
        var found = items.FirstOrDefault(i => i.GetProperty("attemptId").GetGuid() == attemptId);
        Assert.NotEqual(JsonValueKind.Undefined, found.ValueKind);
        Assert.Equal("EXPIRED", found.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, found.GetProperty("result").ValueKind);

        // Verificar que en la base de datos el estado SIGUE SIENDO 'IN_PROGRESS' (lectura pura)
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            var statusInDb = await conn.QuerySingleAsync<string>(
                "SELECT status FROM yusay.assessment_attempt WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });
            Assert.Equal("IN_PROGRESS", statusInDb);
        }
    }

    [Fact]
    public async Task List_WhenAttemptSubmitted_ShouldIncludeResultSummary()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();
        var (instrumentId, _, q1, opt1_1, opt1_2, q2, opt2_1, opt2_2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "hist_sub_sum");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder con score 2
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_2}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_2}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // Consultar listado
        var listResp = await SendGetAsync(client, "/assessments", userToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);

        using var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var item = listDoc.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("attemptId").GetGuid() == attemptId);

        Assert.Equal("SUBMITTED", item.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Object, item.GetProperty("result").ValueKind);
        Assert.Equal(2, item.GetProperty("result").GetProperty("score").GetInt32());
        Assert.Equal("Elevado", item.GetProperty("result").GetProperty("interpretationLabel").GetString());
    }

    [Fact]
    public async Task List_ShouldIsolateAttemptsByOwner_RN025()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken1) = await CreateSessionAsync();
        var (_, userToken2) = await CreateSessionAsync();

        var (inst1, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_iso1");
        var (inst2, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_iso2");

        // User1 inicia intento en inst1
        var resp1 = await SendPostAsync(client, $"/instruments/{inst1}/attempts", userToken1);
        resp1.EnsureSuccessStatusCode();
        using var doc1 = JsonDocument.Parse(await resp1.Content.ReadAsStringAsync());
        var attUser1 = doc1.RootElement.GetProperty("attemptId").GetGuid();

        // User2 inicia intento en inst2
        var resp2 = await SendPostAsync(client, $"/instruments/{inst2}/attempts", userToken2);
        resp2.EnsureSuccessStatusCode();
        using var doc2 = JsonDocument.Parse(await resp2.Content.ReadAsStringAsync());
        var attUser2 = doc2.RootElement.GetProperty("attemptId").GetGuid();

        // User1 consulta listado -> ve solo attUser1
        var listResp1 = await SendGetAsync(client, "/assessments", userToken1);
        using var listDoc1 = JsonDocument.Parse(await listResp1.Content.ReadAsStringAsync());
        var items1 = listDoc1.RootElement.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("attemptId").GetGuid()).ToList();
        Assert.Contains(attUser1, items1);
        Assert.DoesNotContain(attUser2, items1);

        // User2 consulta listado -> ve solo attUser2
        var listResp2 = await SendGetAsync(client, "/assessments", userToken2);
        using var listDoc2 = JsonDocument.Parse(await listResp2.Content.ReadAsStringAsync());
        var items2 = listDoc2.RootElement.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("attemptId").GetGuid()).ToList();
        Assert.Contains(attUser2, items2);
        Assert.DoesNotContain(attUser1, items2);
    }

    [Fact]
    public async Task List_WithAdministratorToken_ShouldNotSeeOtherUsersAttempts_RN026()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();
        var (instId, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_admin_priv");

        // User crea un intento
        var userStartResp = await SendPostAsync(client, $"/instruments/{instId}/attempts", userToken);
        userStartResp.EnsureSuccessStatusCode();
        using var userStartDoc = JsonDocument.Parse(await userStartResp.Content.ReadAsStringAsync());
        var userAttemptId = userStartDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Admin consulta /assessments -> no debe ver el intento privado del usuario
        var adminListResp = await SendGetAsync(client, "/assessments", adminToken);
        Assert.Equal(HttpStatusCode.OK, adminListResp.StatusCode);
        using var adminListDoc = JsonDocument.Parse(await adminListResp.Content.ReadAsStringAsync());
        var adminItems = adminListDoc.RootElement.GetProperty("items").EnumerateArray()
            .Select(x => x.GetProperty("attemptId").GetGuid()).ToList();
        Assert.DoesNotContain(userAttemptId, adminItems);
    }

    [Fact]
    public async Task Detail_WhenSubmitted_ShouldIncludeResultDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();
        var (instrumentId, _, q1, opt1_1, opt1_2, q2, opt2_1, opt2_2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "hist_det_sub");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder con score 1 (opt1_2 = contrib 1, opt2_1 = contrib 0 -> total 1)
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_2}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // Consultar detalle GET /assessments/{attemptId}
        var detailResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);

        using var detailDoc = JsonDocument.Parse(await detailResp.Content.ReadAsStringAsync());
        var root = detailDoc.RootElement;
        Assert.Equal(attemptId, root.GetProperty("attemptId").GetGuid());
        Assert.Equal("SUBMITTED", root.GetProperty("status").GetString());
        Assert.Equal(2, root.GetProperty("questions").GetArrayLength());

        // Verificar result
        Assert.True(root.TryGetProperty("result", out var resultProp));
        Assert.Equal(JsonValueKind.Object, resultProp.ValueKind);
        Assert.Equal(1, resultProp.GetProperty("score").GetInt32());

        var interp = resultProp.GetProperty("interpretation");
        Assert.Equal("Normal", interp.GetProperty("label").GetString());
        Assert.Equal("Rango normal", interp.GetProperty("description").GetString());
        Assert.True(resultProp.TryGetProperty("calculatedAt", out var calcAtProp));
        Assert.NotEqual(default, calcAtProp.GetDateTimeOffset());
    }

    [Fact]
    public async Task Detail_WhenInProgress_ShouldHaveNullResult()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();
        var (instrumentId, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "hist_det_inp");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        var detailResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);

        using var detailDoc = JsonDocument.Parse(await detailResp.Content.ReadAsStringAsync());
        var root = detailDoc.RootElement;
        Assert.Equal("IN_PROGRESS", root.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("result").ValueKind);
    }

    [Fact]
    public async Task History_WhenVersionRetired_ShouldPreserveHistoricalCalculationAndInterpretation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();
        var (instrumentId, versionId, q1, opt1_1, opt1_2, q2, opt2_1, opt2_2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "hist_ret_pres");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder y enviar
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_2}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_2}}" }
            ]
        }
        """);
        await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // Admin retira la versión
        var retireResp = await SendPostAsync(
            client,
            $"/admin/instruments/{instrumentId}/versions/{versionId}/retire",
            adminToken);
        Assert.Equal(HttpStatusCode.OK, retireResp.StatusCode);

        // Usuario consulta listado de historial -> el intento se lista con su resultado
        var listResp = await SendGetAsync(client, "/assessments", userToken);
        Assert.Equal(HttpStatusCode.OK, listResp.StatusCode);
        using var listDoc = JsonDocument.Parse(await listResp.Content.ReadAsStringAsync());
        var item = listDoc.RootElement.GetProperty("items").EnumerateArray()
            .First(i => i.GetProperty("attemptId").GetGuid() == attemptId);
        Assert.Equal("SUBMITTED", item.GetProperty("status").GetString());
        Assert.Equal(2, item.GetProperty("result").GetProperty("score").GetInt32());

        // Usuario consulta detalle del intento -> devuelve preguntas históricas y resultado oficial
        var detailResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        Assert.Equal(HttpStatusCode.OK, detailResp.StatusCode);
        using var detailDoc = JsonDocument.Parse(await detailResp.Content.ReadAsStringAsync());
        var root = detailDoc.RootElement;
        Assert.Equal("SUBMITTED", root.GetProperty("status").GetString());
        Assert.Equal(2, root.GetProperty("result").GetProperty("score").GetInt32());
        Assert.Equal("Elevado", root.GetProperty("result").GetProperty("interpretation").GetProperty("label").GetString());
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
        string? accessToken)
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
        string? accessToken)
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
        string? accessToken)
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
        string email = $"hist_{Guid.NewGuid():N}@yusay.local";
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

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitleSubstring,
        string? expectedDetailSubstring = null)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal((int)expectedStatus, root.GetProperty("status").GetInt32());
        Assert.Contains(expectedTitleSubstring, root.GetProperty("title").GetString()!, StringComparison.OrdinalIgnoreCase);
        if (expectedDetailSubstring is not null)
        {
            Assert.Contains(expectedDetailSubstring, root.GetProperty("detail").GetString()!, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string NewCode(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    private static async Task<Guid> CreateInstrumentAsync(
        HttpClient client,
        string accessToken,
        string code,
        string name,
        string description = "Descripción general",
        string purpose = "Propósito psicométrico")
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
        string sourceDescription = "Fuente psicométrica",
        string population = "Población general",
        string administrationConditions = "Condiciones normales",
        string licenseInformation = "CC-BY",
        string limitations = "Sin limitaciones conocidas")
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

    private async Task<(Guid InstrumentId, Guid VersionId)> CreateAndPublishInstrumentAsync(
        HttpClient client,
        string adminToken,
        string prefix)
    {
        var (instId, verId, _, _, _, _, _, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, prefix);
        return (instId, verId);
    }

    private async Task<(Guid InstrumentId, Guid VersionId, Guid Q1, Guid Opt1_1, Guid Opt1_2, Guid Q2, Guid Opt2_1, Guid Opt2_2)>
        CreateAndPublishInstrumentWithFullOptionsAsync(
            HttpClient client,
            string adminToken,
            string prefix)
    {
        var instId = await CreateInstrumentAsync(client, adminToken, NewCode(prefix), $"Instrument {prefix}");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);

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
                { "label": "Elevado", "description": "Rango elevado", "lowerBound": 2, "upperBound": 2 }
            ]
        }
        """);

        var draftResp = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/admin/instruments/{instId}/versions/{verId}/draft-definition",
            payload,
            adminToken);
        draftResp.EnsureSuccessStatusCode();

        await SendPostAsync(client, $"/admin/instruments/{instId}/versions/{verId}/mark-ready", adminToken);
        await SendPostAsync(client, $"/admin/instruments/{instId}/versions/{verId}/publish", adminToken);

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
