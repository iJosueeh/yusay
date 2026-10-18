using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
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
public sealed class TimelineHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-timeline-http-integration-test-secret-01234567890123456789";

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

    public TimelineHttpTests(PostgreSqlFixture fixture)
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
    public async Task GetTimeline_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resp = await SendGetAsync(client, "/timeline", null);
        await AssertProblemAsync(resp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task GetTimeline_WithGarbageToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resp = await SendGetAsync(client, "/timeline", "garbage-token-12345");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task GetTimeline_WithOutOfRangeLimit_ShouldReturn400ProblemDetails(int limit)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resp = await SendGetAsync(client, $"/timeline?limit={limit}", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El parámetro limit debe ser un entero entre 1 y 100.");
    }

    [Fact]
    public async Task GetTimeline_WithInvalidCursor_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resp = await SendGetAsync(client, "/timeline?cursor=bad-cursor-not-base64url", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El cursor proporcionado no es válido.");
    }

    [Fact]
    public async Task GetTimeline_WithFromLaterThanTo_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var from = DateTimeOffset.UtcNow.AddHours(2).ToString("O");
        var to = DateTimeOffset.UtcNow.ToString("O");

        var resp = await SendGetAsync(client, $"/timeline?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El parámetro 'from' no puede ser posterior al parámetro 'to'.");
    }

    [Fact]
    public async Task GetTimeline_WithInvalidType_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resp = await SendGetAsync(client, "/timeline?type=unknown_event_type", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El parámetro 'type' debe ser 'assessment' o 'check_in'.");
    }

    [Fact]
    public async Task GetTimeline_WhenEmpty_ShouldReturn200WithEmptyItemsAndNullNextCursor()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var resp = await SendGetAsync(client, "/timeline", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(0, items.GetArrayLength());
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task GetTimeline_WithSubmittedAssessmentAndCheckIn_ShouldReturnUnifiedChronologicalFeed()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var adminToken = await CreateAdminSessionAsync();

        var (instId, _, q1, opt1, q2, opt2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "TL1");
        var (dimId, _) = await SeedDimensionAsync("tl_dim1");

        // 1. Check-in anterior (hace 2 horas)
        var checkInId = await CreateCheckInAsync(factory, userToken, dimId, value: 4, note: "CheckIn matutino",
            recordedAt: DateTimeOffset.UtcNow.AddHours(-2));

        // 2. Assessment posterior (hace 30 minutos)
        var attemptId = await StartAttemptAsync(client, instId, userToken);
        await SaveAnswersAsync(client, attemptId, userToken, (q1, opt1), (q2, opt2));
        await SubmitAttemptAsync(client, attemptId, userToken);

        // Consultar Timeline
        var resp = await SendGetAsync(client, "/timeline", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var items = root.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());

        // El primer elemento debe ser el assessment (más reciente)
        var item0 = items[0];
        Assert.Equal(attemptId, item0.GetProperty("eventId").GetGuid());
        Assert.Equal("assessment", item0.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, item0.GetProperty("checkIn").ValueKind);
        var assessment0 = item0.GetProperty("assessment");
        Assert.Equal(attemptId, assessment0.GetProperty("attemptId").GetGuid());
        Assert.Equal(instId, assessment0.GetProperty("instrumentId").GetGuid());
        Assert.Equal(2, assessment0.GetProperty("score").GetInt32());
        Assert.Equal("Elevado", assessment0.GetProperty("interpretation").GetProperty("label").GetString());

        // El segundo elemento debe ser el check-in (más antiguo)
        var item1 = items[1];
        Assert.Equal(checkInId, item1.GetProperty("eventId").GetGuid());
        Assert.Equal("check_in", item1.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Null, item1.GetProperty("assessment").ValueKind);
        var checkIn1 = item1.GetProperty("checkIn");
        Assert.Equal(checkInId, checkIn1.GetProperty("checkInId").GetGuid());
        Assert.Equal("CheckIn matutino", checkIn1.GetProperty("note").GetString());
        var measurements = checkIn1.GetProperty("measurements");
        Assert.Equal(1, measurements.GetArrayLength());
        Assert.Equal(dimId, measurements[0].GetProperty("dimensionId").GetGuid());
        Assert.Equal(4, measurements[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task GetTimeline_WithInProgressOrExpiredAssessment_ShouldExcludeThemFromTimeline()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var adminToken = await CreateAdminSessionAsync();

        var (instId, _, _, _, _, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "TL2");
        var (dimId, _) = await SeedDimensionAsync("tl_dim2");

        // Intento 1: IN_PROGRESS
        var inProgressAttemptId = await StartAttemptAsync(client, instId, userToken);

        // Check-in
        var checkInId = await CreateCheckInAsync(factory, userToken, dimId, value: 3, note: "Único evento");

        var resp = await SendGetAsync(client, "/timeline", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(checkInId, items[0].GetProperty("eventId").GetGuid());
        Assert.Equal("check_in", items[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetTimeline_WithCheckInMeasurements_ShouldReturnMeasurementsWithoutDuplicatingCheckIn()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();

        var (dim1, _) = await SeedDimensionAsync("tl_m1");
        var (dim2, _) = await SeedDimensionAsync("tl_m2");
        var (dim3, _) = await SeedDimensionAsync("tl_m3");

        // Crear CheckIn con 3 mediciones
        var respCheckIn = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/check-ins",
            JsonContent.Create(new
            {
                note = "Triple medición",
                measurements = new[]
                {
                    new { dimensionId = dim1, value = 1 },
                    new { dimensionId = dim2, value = 3 },
                    new { dimensionId = dim3, value = 5 }
                }
            }),
            userToken);
        Assert.Equal(HttpStatusCode.Created, respCheckIn.StatusCode);
        var checkInId = (await respCheckIn.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("checkInId").GetGuid();

        var resp = await SendGetAsync(client, "/timeline", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");

        // Debe haber EXACTAMENTE 1 evento en el timeline, no 3
        Assert.Equal(1, items.GetArrayLength());
        var item = items[0];
        Assert.Equal(checkInId, item.GetProperty("eventId").GetGuid());
        var measurements = item.GetProperty("checkIn").GetProperty("measurements");
        Assert.Equal(3, measurements.GetArrayLength());
    }

    [Fact]
    public async Task GetTimeline_WhenCheckInDeleted_ShouldExcludeItFromTimeline()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tl_del");

        var checkInId = await CreateCheckInAsync(factory, userToken, dimId, value: 2, note: "Para borrar");

        // Verificar que está presente
        var respBefore = await SendGetAsync(client, "/timeline", userToken);
        using var docBefore = JsonDocument.Parse(await respBefore.Content.ReadAsStringAsync());
        Assert.Equal(1, docBefore.RootElement.GetProperty("items").GetArrayLength());

        // Borrar check-in mediante DELETE /check-ins/{id}
        var delResp = await SendJsonAsync(
            client,
            HttpMethod.Delete,
            $"/check-ins/{checkInId}",
            JsonContent.Create(new { revision = 1 }),
            userToken);
        Assert.Equal(HttpStatusCode.NoContent, delResp.StatusCode);

        // Verificar que quedó inmediatamente excluido del timeline
        var respAfter = await SendGetAsync(client, "/timeline", userToken);
        using var docAfter = JsonDocument.Parse(await respAfter.Content.ReadAsStringAsync());
        Assert.Equal(0, docAfter.RootElement.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task GetTimeline_WithFromAndToFilters_ShouldFilterChronologically()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tl_filt");

        var now = DateTimeOffset.UtcNow;
        var ev1 = await CreateCheckInAsync(factory, userToken, dimId, value: 1, note: "Hace 5h", recordedAt: now.AddHours(-5));
        var ev2 = await CreateCheckInAsync(factory, userToken, dimId, value: 2, note: "Hace 3h", recordedAt: now.AddHours(-3));
        var ev3 = await CreateCheckInAsync(factory, userToken, dimId, value: 3, note: "Hace 1h", recordedAt: now.AddHours(-1));

        var from = Uri.EscapeDataString(now.AddHours(-4).ToString("O"));
        var to = Uri.EscapeDataString(now.AddHours(-2).ToString("O"));

        var resp = await SendGetAsync(client, $"/timeline?from={from}&to={to}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(ev2, items[0].GetProperty("eventId").GetGuid());
    }

    [Fact]
    public async Task GetTimeline_WithTypeFilter_ShouldFilterCorrectly()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var adminToken = await CreateAdminSessionAsync();

        var (instId, _, q1, opt1, q2, opt2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "TL_TYPE");
        var (dimId, _) = await SeedDimensionAsync("tl_type_dim");

        var checkInId = await CreateCheckInAsync(factory, userToken, dimId, value: 3);
        var attemptId = await StartAttemptAsync(client, instId, userToken);
        await SaveAnswersAsync(client, attemptId, userToken, (q1, opt1), (q2, opt2));
        await SubmitAttemptAsync(client, attemptId, userToken);

        // Filtrar type=assessment
        var respAssess = await SendGetAsync(client, "/timeline?type=assessment", userToken);
        Assert.Equal(HttpStatusCode.OK, respAssess.StatusCode);
        using var docAssess = JsonDocument.Parse(await respAssess.Content.ReadAsStringAsync());
        var itemsAssess = docAssess.RootElement.GetProperty("items");
        Assert.Equal(1, itemsAssess.GetArrayLength());
        Assert.Equal("assessment", itemsAssess[0].GetProperty("type").GetString());

        // Filtrar type=check_in
        var respCheckIn = await SendGetAsync(client, "/timeline?type=check_in", userToken);
        Assert.Equal(HttpStatusCode.OK, respCheckIn.StatusCode);
        using var docCheckIn = JsonDocument.Parse(await respCheckIn.Content.ReadAsStringAsync());
        var itemsCheckIn = docCheckIn.RootElement.GetProperty("items");
        Assert.Equal(1, itemsCheckIn.GetArrayLength());
        Assert.Equal("check_in", itemsCheckIn[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetTimeline_WithKeysetPagination_ShouldNavigateDeterministicallyAcrossPages()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tl_page");

        var now = DateTimeOffset.UtcNow;
        var ids = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            var id = await CreateCheckInAsync(factory, userToken, dimId, value: 3, note: $"Item {i}", recordedAt: now.AddHours(-i));
            ids.Add(id);
        }

        // Página 1 (limit=2)
        var respP1 = await SendGetAsync(client, "/timeline?limit=2", userToken);
        Assert.Equal(HttpStatusCode.OK, respP1.StatusCode);
        using var docP1 = JsonDocument.Parse(await respP1.Content.ReadAsStringAsync());
        var p1Items = docP1.RootElement.GetProperty("items");
        Assert.Equal(2, p1Items.GetArrayLength());
        Assert.Equal(ids[0], p1Items[0].GetProperty("eventId").GetGuid());
        Assert.Equal(ids[1], p1Items[1].GetProperty("eventId").GetGuid());
        var nextCursor = docP1.RootElement.GetProperty("nextCursor").GetString();
        Assert.NotNull(nextCursor);

        // Página 2 (limit=2 con cursor)
        var respP2 = await SendGetAsync(client, $"/timeline?limit=2&cursor={nextCursor}", userToken);
        Assert.Equal(HttpStatusCode.OK, respP2.StatusCode);
        using var docP2 = JsonDocument.Parse(await respP2.Content.ReadAsStringAsync());
        var p2Items = docP2.RootElement.GetProperty("items");
        Assert.Equal(2, p2Items.GetArrayLength());
        Assert.Equal(ids[2], p2Items[0].GetProperty("eventId").GetGuid());
        Assert.Equal(ids[3], p2Items[1].GetProperty("eventId").GetGuid());
        var nextCursor2 = docP2.RootElement.GetProperty("nextCursor").GetString();
        Assert.NotNull(nextCursor2);

        // Página 3 (limit=2 con cursor)
        var respP3 = await SendGetAsync(client, $"/timeline?limit=2&cursor={nextCursor2}", userToken);
        Assert.Equal(HttpStatusCode.OK, respP3.StatusCode);
        using var docP3 = JsonDocument.Parse(await respP3.Content.ReadAsStringAsync());
        var p3Items = docP3.RootElement.GetProperty("items");
        Assert.Equal(1, p3Items.GetArrayLength());
        Assert.Equal(ids[4], p3Items[0].GetProperty("eventId").GetGuid());
        Assert.Equal(JsonValueKind.Null, docP3.RootElement.GetProperty("nextCursor").ValueKind);
    }

    [Fact]
    public async Task GetTimeline_WithExactTimestampTie_ShouldBreakTieByEventTypeAndEventId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var adminToken = await CreateAdminSessionAsync();

        var (instId, verId, q1, opt1, q2, opt2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "TL_TIE");
        var (dimId, dimVerId) = await SeedDimensionAsync("tl_tie_dim");

        var tieTimestamp = new DateTimeOffset(2026, 10, 18, 14, 0, 0, TimeSpan.Zero);

        // Insertar directamente un CheckIn con timestamp tieTimestamp
        var checkInId = Guid.NewGuid();
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync("""
            INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at, revision, note)
            VALUES (@CheckInId, @UserId, @TieTime, @TieTime, 1, 'CheckIn con empate de hora');
            INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
            VALUES (@CheckInId, @DimId, @DimVerId, 3);
            """,
            new { CheckInId = checkInId, UserId = userId, TieTime = tieTimestamp, DimId = dimId, DimVerId = dimVerId });

        // Insertar un AssessmentAttempt SUBMITTED con ended_at = tieTimestamp
        var attemptId = Guid.NewGuid();
        await conn.ExecuteAsync("""
            INSERT INTO yusay.assessment_attempt (attempt_id, user_id, instrument_id, instrument_version_id, status, started_at, ended_at, expires_at)
            VALUES (@AttemptId, @UserId, @InstId, @VerId, 'SUBMITTED', @TieTime - interval '10 minutes', @TieTime, @TieTime + interval '720 hours');
            
            INSERT INTO yusay.assessment_result (attempt_id, instrument_version_id, score, interpretation_id, calculated_at)
            SELECT @AttemptId, @VerId, 2, interpretation_id, @TieTime
            FROM yusay.interpretation WHERE instrument_version_id = @VerId LIMIT 1;
            """,
            new { AttemptId = attemptId, UserId = userId, InstId = instId, VerId = verId, TieTime = tieTimestamp });

        // En orden DESC: "check_in" > "assessment"
        // Página de limit=1 debe devolver primero check_in
        var resp1 = await SendGetAsync(client, "/timeline?limit=1", userToken);
        Assert.Equal(HttpStatusCode.OK, resp1.StatusCode);
        using var doc1 = JsonDocument.Parse(await resp1.Content.ReadAsStringAsync());
        var items1 = doc1.RootElement.GetProperty("items");
        Assert.Equal(1, items1.GetArrayLength());
        Assert.Equal(checkInId, items1[0].GetProperty("eventId").GetGuid());
        Assert.Equal("check_in", items1[0].GetProperty("type").GetString());
        var nextCursor = doc1.RootElement.GetProperty("nextCursor").GetString();
        Assert.NotNull(nextCursor);

        // Página siguiente con cursor debe devolver assessment
        var resp2 = await SendGetAsync(client, $"/timeline?limit=1&cursor={nextCursor}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        using var doc2 = JsonDocument.Parse(await resp2.Content.ReadAsStringAsync());
        var items2 = doc2.RootElement.GetProperty("items");
        Assert.Equal(1, items2.GetArrayLength());
        Assert.Equal(attemptId, items2[0].GetProperty("eventId").GetGuid());
        Assert.Equal("assessment", items2[0].GetProperty("type").GetString());
    }

    [Fact]
    public async Task GetTimeline_ShouldIsolateEventsByOwner()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userAToken) = await CreateSessionAsync();
        var (_, userBToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tl_iso");

        var userAEventId = await CreateCheckInAsync(factory, userAToken, dimId, value: 4, note: "De Usuario A");
        var userBEventId = await CreateCheckInAsync(factory, userBToken, dimId, value: 2, note: "De Usuario B");

        // Usuario A sólo ve su evento
        var respA = await SendGetAsync(client, "/timeline", userAToken);
        using var docA = JsonDocument.Parse(await respA.Content.ReadAsStringAsync());
        var itemsA = docA.RootElement.GetProperty("items");
        Assert.Equal(1, itemsA.GetArrayLength());
        Assert.Equal(userAEventId, itemsA[0].GetProperty("eventId").GetGuid());

        // Usuario B sólo ve su evento
        var respB = await SendGetAsync(client, "/timeline", userBToken);
        using var docB = JsonDocument.Parse(await respB.Content.ReadAsStringAsync());
        var itemsB = docB.RootElement.GetProperty("items");
        Assert.Equal(1, itemsB.GetArrayLength());
        Assert.Equal(userBEventId, itemsB[0].GetProperty("eventId").GetGuid());
    }

    [Fact]
    public async Task GetTimeline_ShouldBeReadOnly_WithoutGeneratingAuditEvents()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tl_audit");

        await CreateCheckInAsync(factory, userToken, dimId, value: 3);

        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var auditCountBefore = await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM yusay.audit_event");

        // Consultar Timeline
        var resp = await SendGetAsync(client, "/timeline", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var auditCountAfter = await conn.ExecuteScalarAsync<int>("SELECT count(*) FROM yusay.audit_event");
        Assert.Equal(auditCountBefore, auditCountAfter);
    }

    // ------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------

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
        string email = $"tl_user_{Guid.NewGuid():N}@yusay.local";
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

    private async Task<string> CreateAdminSessionAsync()
    {
        var (userId, token) = await CreateSessionAsync();
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
        return token;
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

    private async Task<(Guid DimensionId, Guid VersionId)> SeedDimensionAsync(string prefix)
    {
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var code = NewCode(prefix);

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, @Code, 'Dimensión de prueba', 'Escala 1-5');
            """, new { DimensionId = dimensionId, Code = code });

        await connection.ExecuteAsync("""
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, 1, 'Versión de prueba.', 1, 5, 1, 'ACTIVE');
            """, new { VersionId = versionId, DimensionId = dimensionId });

        return (dimensionId, versionId);
    }

    private static async Task<Guid> CreateCheckInAsync(
        WebApplicationFactory<Program> factory,
        string accessToken,
        Guid dimensionId,
        int value = 3,
        string? note = "Nota",
        DateTimeOffset? recordedAt = null)
    {
        using var client = factory.CreateClient();
        var response = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/check-ins",
            JsonContent.Create(new
            {
                recordedAt,
                note,
                measurements = new[] { new { dimensionId, value } }
            }),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("checkInId").GetGuid();
    }

    private async Task<(Guid InstrumentId, Guid VersionId, Guid Q1, Guid Opt1, Guid Q2, Guid Opt2)>
        CreateAndPublishInstrumentWithFullOptionsAsync(
            HttpClient client,
            string adminToken,
            string prefix)
    {
        var code = NewCode(prefix);
        var respInst = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            JsonContent.Create(new
            {
                code,
                name = $"Instrument {prefix}",
                description = "Desc",
                purpose = "Propósito"
            }),
            adminToken);
        respInst.EnsureSuccessStatusCode();
        var instId = (await respInst.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instrumentId").GetGuid();

        var respVer = await SendJsonAsync(
            client,
            HttpMethod.Post,
            $"/admin/instruments/{instId}/versions",
            JsonContent.Create(new
            {
                version = 1,
                sourceDescription = "Fuente",
                population = "General",
                administrationConditions = "Condiciones",
                licenseInformation = "CC-BY",
                limitations = "Sin límites"
            }),
            adminToken);
        respVer.EnsureSuccessStatusCode();
        var verId = (await respVer.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instrumentVersionId").GetGuid();

        var payload = Json("""
        {
            "references": [
                { "referenceOrder": 1, "citation": "Ref 1", "url": "https://example.com" }
            ],
            "questions": [
                {
                    "position": 1,
                    "prompt": "P1",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "O1A", "contribution": 0 },
                        { "position": 2, "label": "O1B", "contribution": 1 }
                    ]
                },
                {
                    "position": 2,
                    "prompt": "P2",
                    "required": true,
                    "options": [
                        { "position": 1, "label": "O2A", "contribution": 0 },
                        { "position": 2, "label": "O2B", "contribution": 1 }
                    ]
                }
            ],
            "interpretations": [
                { "label": "Normal", "description": "Desc normal", "lowerBound": 0, "upperBound": 1 },
                { "label": "Elevado", "description": "Desc elevado", "lowerBound": 2, "upperBound": 2 }
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

        var q1Id = (Guid)questions[0].question_id;
        var q2Id = (Guid)questions[1].question_id;

        var q1Opts = (await conn.QueryAsync<dynamic>(
            "SELECT option_id, position FROM yusay.answer_option WHERE question_id = @QId ORDER BY position ASC",
            new { QId = q1Id })).ToList();
        var q2Opts = (await conn.QueryAsync<dynamic>(
            "SELECT option_id, position FROM yusay.answer_option WHERE question_id = @QId ORDER BY position ASC",
            new { QId = q2Id })).ToList();

        return (instId, verId, q1Id, (Guid)q1Opts[1].option_id, q2Id, (Guid)q2Opts[1].option_id);
    }

    private static async Task<Guid> StartAttemptAsync(
        HttpClient client,
        Guid instrumentId,
        string userToken)
    {
        var resp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("attemptId").GetGuid();
    }

    private static async Task SaveAnswersAsync(
        HttpClient client,
        Guid attemptId,
        string userToken,
        params (Guid QuestionId, Guid OptionId)[] answers)
    {
        var answersPayload = answers.Select(a => new { questionId = a.QuestionId, optionId = a.OptionId }).ToArray();
        var resp = await SendJsonAsync(
            client,
            HttpMethod.Put,
            $"/assessments/{attemptId}/answers",
            JsonContent.Create(new { answers = answersPayload }),
            userToken);
        resp.EnsureSuccessStatusCode();
    }

    private static async Task SubmitAttemptAsync(
        HttpClient client,
        Guid attemptId,
        string userToken)
    {
        var resp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        resp.EnsureSuccessStatusCode();
    }
}
