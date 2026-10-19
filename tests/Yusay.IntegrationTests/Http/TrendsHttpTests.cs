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
public sealed class TrendsHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-trends-http-integration-test-secret-01234567890123456789";

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

    public TrendsHttpTests(PostgreSqlFixture fixture)
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
    public async Task GetDimensionTrends_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resp = await SendGetAsync(client, $"/trends/dimensions/{Guid.NewGuid()}", null);
        await AssertProblemAsync(resp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task GetInstrumentTrends_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var resp = await SendGetAsync(client, $"/trends/instruments/{Guid.NewGuid()}", null);
        await AssertProblemAsync(resp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task GetDimensionTrends_WithNonExistentDimension_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var nonExistentId = Guid.NewGuid();
        var resp = await SendGetAsync(client, $"/trends/dimensions/{nonExistentId}", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.NotFound, "No encontrado", "La Dimensión solicitada no existe.");
    }

    [Fact]
    public async Task GetInstrumentTrends_WithNonExistentInstrument_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var nonExistentId = Guid.NewGuid();
        var resp = await SendGetAsync(client, $"/trends/instruments/{nonExistentId}", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.NotFound, "No encontrado", "El Instrumento solicitado no existe.");
    }

    [Fact]
    public async Task GetDimensionTrends_WithFromLaterThanTo_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tr_from_to");

        var from = DateTimeOffset.UtcNow.ToString("O");
        var to = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");

        var resp = await SendGetAsync(client, $"/trends/dimensions/{dimId}?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El parámetro 'from' no puede ser posterior al parámetro 'to'.");
    }

    [Fact]
    public async Task GetInstrumentTrends_WithFromLaterThanTo_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();
        var adminToken = await CreateAdminSessionAsync();
        var (instId, _, _, _, _, _) = await CreatePublishedInstrumentAsync(client, adminToken);

        var from = DateTimeOffset.UtcNow.ToString("O");
        var to = DateTimeOffset.UtcNow.AddDays(-1).ToString("O");

        var resp = await SendGetAsync(client, $"/trends/instruments/{instId}?from={Uri.EscapeDataString(from)}&to={Uri.EscapeDataString(to)}", userToken);
        await AssertProblemAsync(resp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "El parámetro 'from' no puede ser posterior al parámetro 'to'.");
    }

    [Fact]
    public async Task GetDimensionTrends_UserIsolation_ShouldOnlyReturnAuthenticatedUserMeasurements()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userAId, userAToken) = await CreateSessionAsync();
        var (userBId, userBToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tr_iso");

        await CreateCheckInAsync(factory, userAToken, dimId, value: 3);
        await CreateCheckInAsync(factory, userBToken, dimId, value: 5);

        // User A consulta tendencias
        var respA = await SendGetAsync(client, $"/trends/dimensions/{dimId}", userAToken);
        Assert.Equal(HttpStatusCode.OK, respA.StatusCode);
        using var docA = JsonDocument.Parse(await respA.Content.ReadAsStringAsync());
        var seriesA = docA.RootElement.GetProperty("series");
        Assert.Equal(1, seriesA.GetArrayLength());
        var pointsA = seriesA[0].GetProperty("points");
        Assert.Equal(1, pointsA.GetArrayLength());
        Assert.Equal(3, pointsA[0].GetProperty("value").GetInt32());

        // User B consulta tendencias
        var respB = await SendGetAsync(client, $"/trends/dimensions/{dimId}", userBToken);
        Assert.Equal(HttpStatusCode.OK, respB.StatusCode);
        using var docB = JsonDocument.Parse(await respB.Content.ReadAsStringAsync());
        var seriesB = docB.RootElement.GetProperty("series");
        Assert.Equal(1, seriesB.GetArrayLength());
        var pointsB = seriesB[0].GetProperty("points");
        Assert.Equal(1, pointsB.GetArrayLength());
        Assert.Equal(5, pointsB[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task GetDimensionTrends_WithValidCheckIns_ShouldReturnPointsAndOrdinalStatistics()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tr_stats");

        // 3 check-ins con valores 2, 4, 1
        await CreateCheckInAsync(factory, userToken, dimId, value: 2);
        await Task.Delay(50);
        await CreateCheckInAsync(factory, userToken, dimId, value: 4);
        await Task.Delay(50);
        await CreateCheckInAsync(factory, userToken, dimId, value: 1);

        var resp = await SendGetAsync(client, $"/trends/dimensions/{dimId}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(dimId, root.GetProperty("dimensionId").GetGuid());
        var series = root.GetProperty("series");
        Assert.Equal(1, series.GetArrayLength());

        var firstSeries = series[0];
        Assert.Equal(1, firstSeries.GetProperty("referenceVersionNumber").GetInt32());
        var points = firstSeries.GetProperty("points");
        Assert.Equal(3, points.GetArrayLength());

        var summary = firstSeries.GetProperty("summary");
        Assert.Equal(3, summary.GetProperty("count").GetInt32());
        Assert.Equal(1, summary.GetProperty("min").GetInt32());
        Assert.Equal(4, summary.GetProperty("max").GetInt32());
        Assert.Equal(2, summary.GetProperty("firstValue").GetInt32());
        Assert.Equal(1, summary.GetProperty("lastValue").GetInt32());
        // Valores ordenados: [1, 2, 4] -> Mediana = 2
        Assert.Equal(2, summary.GetProperty("median").GetInt32());

        // Verificar frecuencias
        var freqs = summary.GetProperty("frequencies");
        Assert.Equal(1, freqs.GetProperty("1").GetInt32());
        Assert.Equal(1, freqs.GetProperty("2").GetInt32());
        Assert.Equal(1, freqs.GetProperty("4").GetInt32());

        // Asegurarse de que NO existen promedio ni delta
        Assert.False(summary.TryGetProperty("average", out _));
        Assert.False(summary.TryGetProperty("delta", out _));
    }

    [Fact]
    public async Task GetDimensionTrends_WithDeletedCheckIn_ShouldExcludeDeletedMeasurements()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();
        var (dimId, _) = await SeedDimensionAsync("tr_del");

        var c1Id = await CreateCheckInAsync(factory, userToken, dimId, value: 2);
        var c2Id = await CreateCheckInAsync(factory, userToken, dimId, value: 4);

        // Eliminar c1
        var delResp = await SendJsonAsync(
            client,
            HttpMethod.Delete,
            $"/check-ins/{c1Id}",
            JsonContent.Create(new { revision = 1 }),
            userToken);
        delResp.EnsureSuccessStatusCode();

        // Consultar tendencias
        var resp = await SendGetAsync(client, $"/trends/dimensions/{dimId}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var series = doc.RootElement.GetProperty("series");
        Assert.Equal(1, series.GetArrayLength());

        var points = series[0].GetProperty("points");
        Assert.Equal(1, points.GetArrayLength());
        Assert.Equal(c2Id, points[0].GetProperty("checkInId").GetGuid());
        Assert.Equal(4, points[0].GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task GetInstrumentTrends_WithSubmittedAssessments_ShouldReturnPointsAndContinuousStatistics()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();
        var adminToken = await CreateAdminSessionAsync();

        var (instId, verId, q1Id, q1Opt2, q2Id, q2Opt2) = await CreatePublishedInstrumentAsync(client, adminToken);

        // Intento 1: Q1=0, Q2=1 -> Score = 1
        var a1Id = await StartAttemptAsync(client, instId, userToken);
        await SaveAnswersAsync(client, a1Id, userToken, (q1Id, q1Opt2), (q2Id, q2Opt2));
        await SubmitAttemptAsync(client, a1Id, userToken);

        // Intento 2: Q1=0, Q2=0 -> Score = 2
        var a2Id = await StartAttemptAsync(client, instId, userToken);
        await SaveAnswersAsync(client, a2Id, userToken, (q1Id, q1Opt2), (q2Id, q2Opt2));
        await SubmitAttemptAsync(client, a2Id, userToken);

        var resp = await SendGetAsync(client, $"/trends/instruments/{instId}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal(instId, root.GetProperty("instrumentId").GetGuid());
        var series = root.GetProperty("series");
        Assert.Equal(1, series.GetArrayLength());

        var s = series[0];
        Assert.Equal(verId, s.GetProperty("referenceVersionId").GetGuid());
        Assert.Equal(1, s.GetProperty("referenceVersionNumber").GetInt32());

        var points = s.GetProperty("points");
        Assert.Equal(2, points.GetArrayLength());

        var summary = s.GetProperty("summary");
        Assert.Equal(2, summary.GetProperty("count").GetInt32());
        Assert.Equal(2, summary.GetProperty("min").GetInt32());
        Assert.Equal(2, summary.GetProperty("max").GetInt32());
        Assert.Equal(2.00m, summary.GetProperty("average").GetDecimal());
        Assert.Equal(0, summary.GetProperty("delta").GetInt32());
    }

    [Fact]
    public async Task GetInstrumentTrends_WithInProgressOrExpiredAttempts_ShouldExcludeNonSubmitted()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();
        var adminToken = await CreateAdminSessionAsync();

        var (instId, _, q1Id, q1Opt2, q2Id, q2Opt2) = await CreatePublishedInstrumentAsync(client, adminToken);

        // Intento 1: SUBMITTED
        var a1Id = await StartAttemptAsync(client, instId, userToken);
        await SaveAnswersAsync(client, a1Id, userToken, (q1Id, q1Opt2), (q2Id, q2Opt2));
        await SubmitAttemptAsync(client, a1Id, userToken);

        // Intento 2: IN_PROGRESS (no enviado)
        var a2Id = await StartAttemptAsync(client, instId, userToken);

        var resp = await SendGetAsync(client, $"/trends/instruments/{instId}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var series = doc.RootElement.GetProperty("series");
        Assert.Equal(1, series.GetArrayLength());

        var points = series[0].GetProperty("points");
        Assert.Equal(1, points.GetArrayLength());
        Assert.Equal(a1Id, points[0].GetProperty("attemptId").GetGuid());
    }

    [Fact]
    public async Task GetDimensionTrends_WithV1V2AndV2V3Compatible_WithoutV1V3_ShouldGenerateTwoOverlappingSeries_WithoutTransitivity()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();

        var dimId = Guid.NewGuid();
        var v1Id = Guid.NewGuid();
        var v2Id = Guid.NewGuid();
        var v3Id = Guid.NewGuid();
        var code = NewCode("tr_cmp");

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, @Code, 'Dimensión Multi-versión', 'Escala');
            """, new { DimensionId = dimId, Code = code });

        await connection.ExecuteAsync("""
            INSERT INTO yusay.dimension_version (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES 
                (@V1, @DimensionId, 1, 'Def V1', 1, 5, 1, 'RETIRED'),
                (@V2, @DimensionId, 2, 'Def V2', 1, 5, 1, 'RETIRED'),
                (@V3, @DimensionId, 3, 'Def V3', 1, 5, 1, 'ACTIVE');
            """, new { DimensionId = dimId, V1 = v1Id, V2 = v2Id, V3 = v3Id });

        // Insertar compatibilidad canónica: (V1, V2) y (V2, V3) usando LEAST/GREATEST
        await connection.ExecuteAsync("""
            INSERT INTO yusay.dimension_version_compatibility (version_a_id, version_b_id, dimension_id, rationale)
            VALUES 
                (LEAST(@V1::uuid, @V2::uuid), GREATEST(@V1::uuid, @V2::uuid), @DimensionId, 'V1-V2 compatible'),
                (LEAST(@V2::uuid, @V3::uuid), GREATEST(@V2::uuid, @V3::uuid), @DimensionId, 'V2-V3 compatible');
            """, new { DimensionId = dimId, V1 = v1Id, V2 = v2Id, V3 = v3Id });

        // Insertar mediciones históricas directas para el usuario
        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();
        var c3 = Guid.NewGuid();
        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await connection.ExecuteAsync("""
            INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at)
            VALUES 
                (@C1, @UserId, @T1, @T1),
                (@C2, @UserId, @T2, @T2),
                (@C3, @UserId, @T3, @T3);
            """, new { UserId = userId, C1 = c1, C2 = c2, C3 = c3, T1 = t1, T2 = t2, T3 = t3 });

        await connection.ExecuteAsync("""
            INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
            VALUES 
                (@C1, @DimensionId, @V1, 2),
                (@C2, @DimensionId, @V2, 3),
                (@C3, @DimensionId, @V3, 4);
            """, new { DimensionId = dimId, C1 = c1, C2 = c2, C3 = c3, V1 = v1Id, V2 = v2Id, V3 = v3Id });

        // Consultar tendencias
        var resp = await SendGetAsync(client, $"/trends/dimensions/{dimId}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var series = doc.RootElement.GetProperty("series");

        // Debe generar exactamente 2 series
        Assert.Equal(2, series.GetArrayLength());

        var s1 = series[0];
        Assert.Equal(v2Id, s1.GetProperty("referenceVersionId").GetGuid());
        Assert.Equal(2, s1.GetProperty("referenceVersionNumber").GetInt32());
        var pts1 = s1.GetProperty("points");
        Assert.Equal(2, pts1.GetArrayLength());
        Assert.Equal(c1, pts1[0].GetProperty("checkInId").GetGuid());
        Assert.Equal(c2, pts1[1].GetProperty("checkInId").GetGuid());

        var s2 = series[1];
        Assert.Equal(v3Id, s2.GetProperty("referenceVersionId").GetGuid());
        Assert.Equal(3, s2.GetProperty("referenceVersionNumber").GetInt32());
        var pts2 = s2.GetProperty("points");
        Assert.Equal(2, pts2.GetArrayLength());
        Assert.Equal(c2, pts2[0].GetProperty("checkInId").GetGuid());
        Assert.Equal(c3, pts2[1].GetProperty("checkInId").GetGuid());

        // C2 aparece en ambas series
        // C1 y C3 NUNCA aparecen juntas (No transitividad de DR-DOM-003)
    }

    [Fact]
    public async Task GetDimensionTrends_WithTieBreak_ShouldSortByRecordedAtAscThenCheckInIdAsc()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (userId, userToken) = await CreateSessionAsync();
        var (dimId, verId) = await SeedDimensionAsync("tr_tie");

        var sameTime = DateTimeOffset.UtcNow.AddHours(-1);
        var id1 = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var id2 = Guid.Parse("00000000-0000-0000-0000-000000000002");

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        // Insertar en orden inverso (id2 primero, id1 después)
        await connection.ExecuteAsync("""
            INSERT INTO yusay.check_in (check_in_id, user_id, recorded_at, created_at)
            VALUES 
                (@Id2, @UserId, @T, @T),
                (@Id1, @UserId, @T, @T);
            """, new { UserId = userId, Id1 = id1, Id2 = id2, T = sameTime });

        await connection.ExecuteAsync("""
            INSERT INTO yusay.measurement (check_in_id, dimension_id, dimension_version_id, value)
            VALUES 
                (@Id2, @DimensionId, @VerId, 4),
                (@Id1, @DimensionId, @VerId, 2);
            """, new { DimensionId = dimId, VerId = verId, Id1 = id1, Id2 = id2 });

        var resp = await SendGetAsync(client, $"/trends/dimensions/{dimId}", userToken);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var pts = doc.RootElement.GetProperty("series")[0].GetProperty("points");
        Assert.Equal(2, pts.GetArrayLength());
        Assert.Equal(id1, pts[0].GetProperty("checkInId").GetGuid());
        Assert.Equal(id2, pts[1].GetProperty("checkInId").GetGuid());
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
        string email = $"tr_user_{Guid.NewGuid():N}@yusay.local";
        var registration = await new RegisterUserUseCase(
                _unitOfWork, _userAccountRepo, _userCredentialRepo, _verificationTokenRepo,
                _auditEventRepo, _passwordHasher, new SecureTokenService(), new NullEmailVerificationSender())
            .ExecuteAsync(new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));
        await new VerifyEmailUseCase(
                _unitOfWork, _userAccountRepo, _verificationTokenRepo, _auditEventRepo, new SecureTokenService())
            .ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));
        var auth = await new SignInUseCase(
                _unitOfWork, _userAccountRepo, _userCredentialRepo, _auditEventRepo, _passwordHasher, _jwtTokenService)
            .ExecuteAsync(new SignInCommand(email, ValidPassword));
        return (auth.UserId, auth.AccessToken);
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
        string token,
        Guid dimensionId,
        int value)
    {
        using var client = factory.CreateClient();
        var payload = JsonContent.Create(new
        {
            recordedAt = DateTimeOffset.UtcNow,
            note = "Nota de seguimiento",
            measurements = new[]
            {
                new { dimensionId, value }
            }
        });

        var resp = await SendJsonAsync(client, HttpMethod.Post, "/check-ins", payload, token);
        resp.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("checkInId").GetGuid();
    }

    private async Task<(Guid InstrumentId, Guid VersionId, Guid Q1Id, Guid Q1Opt2, Guid Q2Id, Guid Q2Opt2)>
        CreatePublishedInstrumentAsync(HttpClient client, string adminToken)
    {
        var instCode = NewCode("inst");
        var respInst = await SendJsonAsync(
            client,
            HttpMethod.Post,
            "/admin/instruments",
            JsonContent.Create(new
            {
                code = instCode,
                name = $"Instrument {instCode}",
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
