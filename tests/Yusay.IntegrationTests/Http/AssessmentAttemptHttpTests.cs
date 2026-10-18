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
using Yusay.Application.Assessment.Repositories;
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
public sealed class AssessmentAttemptHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-assessment-attempt-http-integration-test-secret-0123456789";

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

    public AssessmentAttemptHttpTests(PostgreSqlFixture fixture)
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

        var response = await SendPostAsync(client, $"/instruments/{Guid.NewGuid()}/attempts", null);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task StartAttempt_WhenInstrumentNotFoundOrNotPublished_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, standardToken) = await CreateSessionAsync();

        // 1. Non-existent instrument
        var notFoundResp = await SendPostAsync(client, $"/instruments/{Guid.NewGuid()}/attempts", standardToken);
        await AssertProblemAsync(notFoundResp, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El instrumento solicitado no existe o no tiene una versión publicada.");

        // 2. Instrument with only DRAFT version
        var draftInst = await CreateInstrumentAsync(client, adminToken, NewCode("att_draft"), "Draft Instrument");
        await CreateVersionAsync(client, adminToken, draftInst, 1);

        var draftResp = await SendPostAsync(client, $"/instruments/{draftInst}/attempts", standardToken);
        await AssertProblemAsync(draftResp, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El instrumento solicitado no existe o no tiene una versión publicada.");

        // 3. Instrument with only READY version
        var readyInst = await CreateInstrumentAsync(client, adminToken, NewCode("att_ready"), "Ready Instrument");
        var readyVer = await CreateVersionAsync(client, adminToken, readyInst, 1);
        await PutValidCompositionAsync(client, adminToken, readyInst, readyVer);
        await MarkReadyAsync(client, adminToken, readyInst, readyVer);

        var readyResp = await SendPostAsync(client, $"/instruments/{readyInst}/attempts", standardToken);
        await AssertProblemAsync(readyResp, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El instrumento solicitado no existe o no tiene una versión publicada.");

        // 4. Instrument with only RETIRED version
        var retiredInst = await CreateInstrumentAsync(client, adminToken, NewCode("att_ret"), "Retired Instrument");
        var retiredVer = await CreateVersionAsync(client, adminToken, retiredInst, 1);
        await PutValidCompositionAsync(client, adminToken, retiredInst, retiredVer);
        await MarkReadyAsync(client, adminToken, retiredInst, retiredVer);
        await PublishAsync(client, adminToken, retiredInst, retiredVer);
        await RetireAsync(client, adminToken, retiredInst, retiredVer);

        var retiredResp = await SendPostAsync(client, $"/instruments/{retiredInst}/attempts", standardToken);
        await AssertProblemAsync(retiredResp, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El instrumento solicitado no existe o no tiene una versión publicada.");
    }

    [Fact]
    public async Task StartAttempt_WhenSuccessful_ShouldReturn201WithLocationQuestionsAndOptionsWithoutScoringAndZeroAuditRows()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (userId, standardToken) = await CreateSessionAsync();

        var instId = await CreateInstrumentAsync(client, adminToken, NewCode("att_ok"), "Instrumento Normal");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);
        await PutValidCompositionAsync(client, adminToken, instId, verId);
        await MarkReadyAsync(client, adminToken, instId, verId);
        await PublishAsync(client, adminToken, instId, verId);

        // Count audit events before
        var auditCountBefore = await GetAuditEventCountAsync();

        // Act: Standard user starts attempt
        var response = await SendPostAsync(client, $"/instruments/{instId}/attempts", standardToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var rawJson = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var attemptId = root.GetProperty("attemptId").GetGuid();
        Assert.NotEqual(Guid.Empty, attemptId);
        Assert.Equal(instId, root.GetProperty("instrumentId").GetGuid());
        Assert.Equal(verId, root.GetProperty("instrumentVersionId").GetGuid());
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("IN_PROGRESS", root.GetProperty("status").GetString());

        var startedAt = root.GetProperty("startedAt").GetDateTimeOffset();
        var expiresAt = root.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.Equal(startedAt.AddHours(720), expiresAt);

        // Location header validation
        Assert.NotNull(response.Headers.Location);
        Assert.Equal($"/assessments/{attemptId}", response.Headers.Location.OriginalString);

        // Questions and options validation
        var questions = root.GetProperty("questions").EnumerateArray().ToList();
        Assert.Single(questions);
        var q1 = questions[0];
        Assert.Equal(1, q1.GetProperty("position").GetInt32());
        Assert.Equal("¿Poco interés o alegría?", q1.GetProperty("prompt").GetString());
        Assert.True(q1.GetProperty("required").GetBoolean());

        var options = q1.GetProperty("options").EnumerateArray().ToList();
        Assert.Equal(2, options.Count);
        Assert.Equal(1, options[0].GetProperty("position").GetInt32());
        Assert.Equal("Nunca", options[0].GetProperty("label").GetString());
        Assert.Equal(2, options[1].GetProperty("position").GetInt32());
        Assert.Equal("Varios días", options[1].GetProperty("label").GetString());

        // Absence of scoring, contributions and interpretations
        Assert.DoesNotContain("contribution", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("scoring", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interpretations", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("lowerBound", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("upperBound", rawJson, StringComparison.OrdinalIgnoreCase);

        // Zero audit events recorded for this attempt / user
        var auditCountAfter = await GetAuditEventCountAsync();
        Assert.Equal(auditCountBefore, auditCountAfter);

        var auditAttemptEvents = await GetAuditEventsForEntityAsync(attemptId.ToString());
        Assert.Empty(auditAttemptEvents);
    }

    [Fact]
    public async Task StartAttempt_WhenActiveAttemptExists_ShouldReturn409Conflict()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (user1Id, user1Token) = await CreateSessionAsync();
        var (user2Id, user2Token) = await CreateSessionAsync();

        var inst1 = await CreateInstrumentAsync(client, adminToken, NewCode("att_cnf1"), "Instrumento Conflicto 1");
        var ver1 = await CreateVersionAsync(client, adminToken, inst1, 1);
        await PutValidCompositionAsync(client, adminToken, inst1, ver1);
        await MarkReadyAsync(client, adminToken, inst1, ver1);
        await PublishAsync(client, adminToken, inst1, ver1);

        var inst2 = await CreateInstrumentAsync(client, adminToken, NewCode("att_cnf2"), "Instrumento Conflicto 2");
        var ver2 = await CreateVersionAsync(client, adminToken, inst2, 1);
        await PutValidCompositionAsync(client, adminToken, inst2, ver2);
        await MarkReadyAsync(client, adminToken, inst2, ver2);
        await PublishAsync(client, adminToken, inst2, ver2);

        // User 1 starts attempt on Inst 1 -> 201
        var firstResp = await SendPostAsync(client, $"/instruments/{inst1}/attempts", user1Token);
        Assert.Equal(HttpStatusCode.Created, firstResp.StatusCode);

        // User 1 attempts again on Inst 1 -> 409 Conflict
        var secondResp = await SendPostAsync(client, $"/instruments/{inst1}/attempts", user1Token);
        await AssertProblemAsync(secondResp, HttpStatusCode.Conflict, "Conflicto",
            IAssessmentAttemptRepository.ActiveAttemptConflictMessage);

        // User 2 can start attempt on Inst 1 -> 201 (different user)
        var user2Resp = await SendPostAsync(client, $"/instruments/{inst1}/attempts", user2Token);
        Assert.Equal(HttpStatusCode.Created, user2Resp.StatusCode);

        // User 1 can start attempt on Inst 2 -> 201 (different instrument)
        var user1OtherResp = await SendPostAsync(client, $"/instruments/{inst2}/attempts", user1Token);
        Assert.Equal(HttpStatusCode.Created, user1OtherResp.StatusCode);
    }

    [Fact]
    public async Task StartAttempt_WhenPreviousAttemptExpired_ShouldPerformLazyExpirationAndReturn201()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (userId, userToken) = await CreateSessionAsync();

        var instId = await CreateInstrumentAsync(client, adminToken, NewCode("att_exp"), "Instrumento Expiración");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);
        await PutValidCompositionAsync(client, adminToken, instId, verId);
        await MarkReadyAsync(client, adminToken, instId, verId);
        await PublishAsync(client, adminToken, instId, verId);

        // 1. Create first attempt
        var firstResp = await SendPostAsync(client, $"/instruments/{instId}/attempts", userToken);
        Assert.Equal(HttpStatusCode.Created, firstResp.StatusCode);
        using var firstDoc = JsonDocument.Parse(await firstResp.Content.ReadAsStringAsync());
        var oldAttemptId = firstDoc.RootElement.GetProperty("attemptId").GetGuid();

        // 2. Retrieve question and option ID from DB to insert a dummy answer
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        var questionId = await conn.QuerySingleAsync<Guid>(
            "SELECT question_id FROM yusay.question WHERE instrument_version_id = @VerId LIMIT 1;",
            new { VerId = verId });
        var optionId = await conn.QuerySingleAsync<Guid>(
            "SELECT option_id FROM yusay.answer_option WHERE question_id = @QuestionId LIMIT 1;",
            new { QuestionId = questionId });

        await conn.ExecuteAsync(
            """
            INSERT INTO yusay.answer (attempt_id, instrument_version_id, question_id, option_id)
            VALUES (@AttemptId, @VerId, @QuestionId, @OptionId);
            """,
            new { AttemptId = oldAttemptId, VerId = verId, QuestionId = questionId, OptionId = optionId });

        // Confirm answer exists
        var answerCountBefore = await conn.QuerySingleAsync<int>(
            "SELECT count(*) FROM yusay.answer WHERE attempt_id = @AttemptId;",
            new { AttemptId = oldAttemptId });
        Assert.Equal(1, answerCountBefore);

        // 3. Age the attempt so it is past expires_at (721 hours in the past)
        await conn.ExecuteAsync(
            """
            UPDATE yusay.assessment_attempt
            SET started_at = started_at - INTERVAL '721 hours',
                expires_at = expires_at - INTERVAL '721 hours'
            WHERE attempt_id = @AttemptId;
            """,
            new { AttemptId = oldAttemptId });

        // 4. Start attempt again as the same user
        var secondResp = await SendPostAsync(client, $"/instruments/{instId}/attempts", userToken);
        if (secondResp.StatusCode != HttpStatusCode.Created)
        {
            var body = await secondResp.Content.ReadAsStringAsync();
            Assert.Fail($"Expected Created but got {secondResp.StatusCode}. Body: {body}");
        }
        Assert.Equal(HttpStatusCode.Created, secondResp.StatusCode);
        using var secondDoc = JsonDocument.Parse(await secondResp.Content.ReadAsStringAsync());
        var newAttemptId = secondDoc.RootElement.GetProperty("attemptId").GetGuid();
        Assert.NotEqual(oldAttemptId, newAttemptId);

        // 5. Verify old attempt state in DB: EXPIRED, ended_at = expires_at, answers deleted
        var oldAttemptRow = await conn.QuerySingleAsync(
            """
            SELECT status, started_at, expires_at, ended_at
            FROM yusay.assessment_attempt
            WHERE attempt_id = @AttemptId;
            """,
            new { AttemptId = oldAttemptId });

        Assert.Equal("EXPIRED", (string)oldAttemptRow.status);
        Assert.NotNull(oldAttemptRow.ended_at);
        Assert.Equal((DateTimeOffset)oldAttemptRow.expires_at, (DateTimeOffset)oldAttemptRow.ended_at);

        var answerCountAfter = await conn.QuerySingleAsync<int>(
            "SELECT count(*) FROM yusay.answer WHERE attempt_id = @AttemptId;",
            new { AttemptId = oldAttemptId });
        Assert.Equal(0, answerCountAfter);

        // 6. Verify new attempt state in DB: IN_PROGRESS, ended_at IS NULL
        var newAttemptRow = await conn.QuerySingleAsync(
            """
            SELECT status, ended_at
            FROM yusay.assessment_attempt
            WHERE attempt_id = @AttemptId;
            """,
            new { AttemptId = newAttemptId });

        Assert.Equal("IN_PROGRESS", (string)newAttemptRow.status);
        Assert.Null(newAttemptRow.ended_at);
    }

    [Fact]
    public async Task StartAttempt_ConcurrentRequestsFromSameUser_ShouldSerializeAndReturnExactlyOne201AndOne409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (userId, userToken) = await CreateSessionAsync();

        var instId = await CreateInstrumentAsync(client, adminToken, NewCode("att_cnc"), "Instrumento Concurrencia");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);
        await PutValidCompositionAsync(client, adminToken, instId, verId);
        await MarkReadyAsync(client, adminToken, instId, verId);
        await PublishAsync(client, adminToken, instId, verId);

        // Run 2 concurrent attempts from same user
        var task1 = SendPostAsync(client, $"/instruments/{instId}/attempts", userToken);
        var task2 = SendPostAsync(client, $"/instruments/{instId}/attempts", userToken);

        var responses = await Task.WhenAll(task1, task2);
        var statusCodes = responses.Select(r => r.StatusCode).ToList();

        Assert.Contains(HttpStatusCode.Created, statusCodes);
        Assert.Contains(HttpStatusCode.Conflict, statusCodes);

        // Exactly one attempt in DB
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var inProgressCount = await conn.QuerySingleAsync<int>(
            """
            SELECT count(*) FROM yusay.assessment_attempt
            WHERE user_id = @UserId AND instrument_id = @InstId AND status = 'IN_PROGRESS';
            """,
            new { UserId = userId, InstId = instId });
        Assert.Equal(1, inProgressCount);
    }

    [Fact]
    public async Task StartAttempt_ConcurrentWithAdminRetire_ShouldSerializeWithoutDeadlock()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (userId, userToken) = await CreateSessionAsync();

        var instId = await CreateInstrumentAsync(client, adminToken, NewCode("att_ret_cnc"), "Instrumento Retiro Concurrente");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);
        await PutValidCompositionAsync(client, adminToken, instId, verId);
        await MarkReadyAsync(client, adminToken, instId, verId);
        await PublishAsync(client, adminToken, instId, verId);

        // Launch concurrent attempt creation and retirement
        var attemptTask = SendPostAsync(client, $"/instruments/{instId}/attempts", userToken);
        var retireTask = RetireAsync(client, adminToken, instId, verId);

        // Neither should throw a deadlock exception (500 or crash)
        var attemptResp = await attemptTask;
        await retireTask;

        // Attempt must be either 201 Created (if attempt locked version first) or 404 Not Found (if retire updated first)
        Assert.True(
            attemptResp.StatusCode == HttpStatusCode.Created || attemptResp.StatusCode == HttpStatusCode.NotFound,
            $"Expected 201 or 404, but got {attemptResp.StatusCode}");
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static string NewCode(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..30];

    private static StringContent Json(string content) =>
        new(content, Encoding.UTF8, "application/json");

    private static async Task<HttpResponseMessage> SendPostAsync(
        HttpClient client,
        string url,
        string? accessToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        if (accessToken is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return await client.SendAsync(req);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        HttpContent content,
        string? accessToken)
    {
        using var req = new HttpRequestMessage(method, url) { Content = content };
        if (accessToken is not null)
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return await client.SendAsync(req);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync()
    {
        string email = $"assessment_attempt_{Guid.NewGuid():N}@yusay.local";
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

    private async Task<int> GetAuditEventCountAsync()
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*) FROM yusay.audit_event;");
    }

    private async Task<IEnumerable<dynamic>> GetAuditEventsForEntityAsync(string entityId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.QueryAsync(
            "SELECT * FROM yusay.audit_event WHERE target_identifier = @EntityId;",
            new { EntityId = entityId });
    }

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
        if (expectedStatus != response.StatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.Fail($"Expected {(int)expectedStatus} {expectedStatus} but got {(int)response.StatusCode} {response.StatusCode}. Body: {body}");
        }

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
