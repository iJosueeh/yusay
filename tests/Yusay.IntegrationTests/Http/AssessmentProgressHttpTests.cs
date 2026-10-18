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
public sealed class AssessmentProgressHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-assessment-progress-http-integration-test-secret-0123456789";

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

    public AssessmentProgressHttpTests(PostgreSqlFixture fixture)
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

        var attemptId = Guid.NewGuid();

        // 1. GET /assessments/{attemptId}
        var getResp = await SendGetAsync(client, $"/assessments/{attemptId}", null);
        await AssertProblemAsync(getResp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);

        // 2. PUT /assessments/{attemptId}/answers
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", Json("{}"), null);
        await AssertProblemAsync(putResp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task Endpoints_WhenAttemptNotFoundOrBelongsToAnotherUser_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken1) = await CreateSessionAsync();
        var (_, userToken2) = await CreateSessionAsync();

        // Crear y publicar instrumento
        var (instrumentId, _, q1, opt1, _, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_priv");

        // Iniciar intento con user1
        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken1);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // User2 intenta consultar intento de User1 -> 404
        var getResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken2);
        await AssertProblemAsync(getResp, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El intento de evaluación solicitado no existe.");

        // User2 intenta responder intento de User1 -> 404
        var answersPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", answersPayload, userToken2);
        await AssertProblemAsync(putResp, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El intento de evaluación solicitado no existe.");

        // Intento inexistente con user1 -> 404
        var nonExistentId = Guid.NewGuid();
        var getNonExistent = await SendGetAsync(client, $"/assessments/{nonExistentId}", userToken1);
        await AssertProblemAsync(getNonExistent, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El intento de evaluación solicitado no existe.");
    }

    [Fact]
    public async Task PutAnswers_ValidationErrors_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1, q2, opt2) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_val");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // 1. Answers vacías
        var emptyAnswersResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", Json("""{"answers":[]}"""), userToken);
        await AssertProblemAsync(emptyAnswersResp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "Debe proporcionar al menos una respuesta para guardar.");

        // 2. Duplicado en la misma pregunta en el request
        var dupPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" },
                { "questionId": "{{q1}}", "optionId": "{{opt2}}" }
            ]
        }
        """);
        var dupResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", dupPayload, userToken);
        await AssertProblemAsync(dupResp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "No se permiten múltiples respuestas para la misma pregunta");

        // 3. Pregunta ajena (no en versión)
        var foreignQuestionId = Guid.NewGuid();
        var foreignQPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{foreignQuestionId}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var foreignQResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", foreignQPayload, userToken);
        await AssertProblemAsync(foreignQResp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "La pregunta especificada no pertenece a la versión del instrumento en evaluación.");

        // 4. Opción ajena (opción de Q2 enviada para Q1)
        var invalidOptPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt2}}" }
            ]
        }
        """);
        var invalidOptResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", invalidOptPayload, userToken);
        await AssertProblemAsync(invalidOptResp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "La opción seleccionada no es válida para la pregunta indicada.");

        // Verificar que no se guardó ninguna respuesta tras los errores 400 (rollback garantizado)
        var getResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        getResp.EnsureSuccessStatusCode();
        using var getDoc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        foreach (var q in getDoc.RootElement.GetProperty("questions").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, q.GetProperty("selectedOptionId").ValueKind);
        }
    }

    [Fact]
    public async Task SaveAnswers_PartialSaveAndUpsert_ShouldSucceedAndPersistAnswers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1, q2, opt2) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_upsert");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // 1. Guardar parcialmente solo Q1
        var put1Payload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var put1Resp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", put1Payload, userToken);
        Assert.Equal(HttpStatusCode.OK, put1Resp.StatusCode);
        using var put1Doc = JsonDocument.Parse(await put1Resp.Content.ReadAsStringAsync());
        Assert.Equal(attemptId, put1Doc.RootElement.GetProperty("attemptId").GetGuid());
        Assert.Equal(1, put1Doc.RootElement.GetProperty("savedCount").GetInt32());

        // Consultar con GET y verificar que Q1 está respondida y Q2 está sin responder
        var get1Resp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        get1Resp.EnsureSuccessStatusCode();
        using var get1Doc = JsonDocument.Parse(await get1Resp.Content.ReadAsStringAsync());
        var questions1 = get1Doc.RootElement.GetProperty("questions").EnumerateArray().ToList();
        var q1Elem = questions1.First(q => q.GetProperty("questionId").GetGuid() == q1);
        var q2Elem = questions1.First(q => q.GetProperty("questionId").GetGuid() == q2);
        Assert.Equal(opt1, q1Elem.GetProperty("selectedOptionId").GetGuid());
        Assert.Equal(JsonValueKind.Null, q2Elem.GetProperty("selectedOptionId").ValueKind);

        // Verificar que no se expone información de scoring ni interpretaciones
        var rawJson = await get1Resp.Content.ReadAsStringAsync();
        Assert.DoesNotContain("score", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("contribution", rawJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("interpretation", rawJson, StringComparison.OrdinalIgnoreCase);

        // 2. Guardar Q2
        var put2Payload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q2}}", "optionId": "{{opt2}}" }
            ]
        }
        """);
        var put2Resp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", put2Payload, userToken);
        Assert.Equal(HttpStatusCode.OK, put2Resp.StatusCode);
        using var put2Doc = JsonDocument.Parse(await put2Resp.Content.ReadAsStringAsync());
        Assert.Equal(1, put2Doc.RootElement.GetProperty("savedCount").GetInt32());

        // Consultar con GET y verificar que ambas están respondidas
        var get2Resp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        get2Resp.EnsureSuccessStatusCode();
        using var get2Doc = JsonDocument.Parse(await get2Resp.Content.ReadAsStringAsync());
        var questions2 = get2Doc.RootElement.GetProperty("questions").EnumerateArray().ToList();
        var q1Elem2 = questions2.First(q => q.GetProperty("questionId").GetGuid() == q1);
        var q2Elem2 = questions2.First(q => q.GetProperty("questionId").GetGuid() == q2);
        Assert.Equal(opt1, q1Elem2.GetProperty("selectedOptionId").GetGuid());
        Assert.Equal(opt2, q2Elem2.GetProperty("selectedOptionId").GetGuid());
    }

    [Fact]
    public async Task Continuity_WhenInstrumentVersionRetired_AttemptCanStillBeViewedAndAnswered()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, versionId, q1, opt1, _, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_ret");

        // Crear intento con versión PUBLISHED
        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Retirar la versión del instrumento
        await RetireAsync(client, adminToken, instrumentId, versionId);

        // El usuario todavía puede responder el intento existente
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // El usuario todavía puede consultar su progreso
        var getResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        using var getDoc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        var questions = getDoc.RootElement.GetProperty("questions").EnumerateArray().ToList();
        var q1Elem = questions.First(q => q.GetProperty("questionId").GetGuid() == q1);
        Assert.Equal(opt1, q1Elem.GetProperty("selectedOptionId").GetGuid());
    }

    [Fact]
    public async Task LazyExpiration_OnPut_ShouldCommitExpirationAndReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1, _, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_lazyp");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Guardar una respuesta previa
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Envejecer el intento forzando expires_at en el pasado
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync(
                """
                UPDATE yusay.assessment_attempt
                SET started_at = started_at - INTERVAL '721 hours',
                    expires_at = expires_at - INTERVAL '721 hours'
                WHERE attempt_id = @AttemptId
                """,
                new { AttemptId = attemptId });
        }

        // Nuevo PUT debe recibir 409 Conflict
        var expiredPutPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var expiredPutResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", expiredPutPayload, userToken);
        await AssertProblemAsync(expiredPutResp, HttpStatusCode.Conflict, "Conflicto",
            "El intento de evaluación ha expirado y no admite más respuestas.");

        // Verificar persistencia en base de datos: status EXPIRED, ended_at no nulo, y 0 respuestas
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            var attemptRow = await conn.QuerySingleAsync<dynamic>(
                "SELECT status, started_at, expires_at, ended_at FROM yusay.assessment_attempt WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });

            Assert.Equal("EXPIRED", (string)attemptRow.status);
            Assert.NotNull(attemptRow.ended_at);

            int answersCount = await conn.QuerySingleAsync<int>(
                "SELECT count(*) FROM yusay.answer WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });
            Assert.Equal(0, answersCount);
        }
    }

    [Fact]
    public async Task LazyExpiration_OnGet_ShouldCommitExpirationAndReturnExpiredStatusWithNoAnswers()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1, _, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_lazyg");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Guardar una respuesta inicial
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Envejecer el intento
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync(
                """
                UPDATE yusay.assessment_attempt
                SET started_at = started_at - INTERVAL '721 hours',
                    expires_at = expires_at - INTERVAL '721 hours'
                WHERE attempt_id = @AttemptId
                """,
                new { AttemptId = attemptId });
        }

        // Llamar a GET: debe devolver 200 OK con estado EXPIRED y respuestas nulas
        var getResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
        using var getDoc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        Assert.Equal("EXPIRED", getDoc.RootElement.GetProperty("status").GetString());

        foreach (var q in getDoc.RootElement.GetProperty("questions").EnumerateArray())
        {
            Assert.Equal(JsonValueKind.Null, q.GetProperty("selectedOptionId").ValueKind);
        }

        // Verificar en DB que la expiración quedó confirmada y las respuestas borradas
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            var attemptRow = await conn.QuerySingleAsync<dynamic>(
                "SELECT status, ended_at FROM yusay.assessment_attempt WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });

            Assert.Equal("EXPIRED", (string)attemptRow.status);
            Assert.NotNull(attemptRow.ended_at);

            int answersCount = await conn.QuerySingleAsync<int>(
                "SELECT count(*) FROM yusay.answer WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });
            Assert.Equal(0, answersCount);
        }
    }

    [Fact]
    public async Task ConcurrentPuts_ShouldSerializeAndSucceedWithoutDeadlocks()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1, q2, opt2) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_conc");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Enviar concurrentemente 5 solicitudes PUT
        var tasks = Enumerable.Range(1, 5).Select(async i =>
        {
            using var localClient = factory.CreateClient();
            var payload = Json($$"""
            {
                "answers": [
                    { "questionId": "{{q1}}", "optionId": "{{opt1}}" },
                    { "questionId": "{{q2}}", "optionId": "{{opt2}}" }
                ]
            }
            """);
            return await SendJsonAsync(localClient, HttpMethod.Put, $"/assessments/{attemptId}/answers", payload, userToken);
        });

        var results = await Task.WhenAll(tasks);

        foreach (var resp in results)
        {
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        }

        // Comprobar estado final consistente
        var getResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        getResp.EnsureSuccessStatusCode();
        using var getDoc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());
        var questions = getDoc.RootElement.GetProperty("questions").EnumerateArray().ToList();
        var q1Elem = questions.First(q => q.GetProperty("questionId").GetGuid() == q1);
        var q2Elem = questions.First(q => q.GetProperty("questionId").GetGuid() == q2);
        Assert.Equal(opt1, q1Elem.GetProperty("selectedOptionId").GetGuid());
        Assert.Equal(opt2, q2Elem.GetProperty("selectedOptionId").GetGuid());
    }

    [Fact]
    public async Task AssessmentProgressOperations_ShouldNotProduceAuditEvents()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1, _, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "prog_audit");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        int auditCountBefore = await GetAuditEventCountAsync();

        // 1. Guardar respuestas
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // 2. Consultar evaluación
        var getResp = await SendGetAsync(client, $"/assessments/{attemptId}", userToken);
        Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);

        int auditCountAfter = await GetAuditEventCountAsync();
        Assert.Equal(auditCountBefore, auditCountAfter);

        var attemptEvents = await GetAuditEventsForEntityAsync(attemptId.ToString());
        Assert.Empty(attemptEvents);
    }

    private static string NewCode(string prefix) => $"{prefix}_{Guid.NewGuid():N}"[..20];

    private async Task<(Guid InstrumentId, Guid VersionId, Guid Q1, Guid Opt1, Guid Q2, Guid Opt2)> CreateAndPublishInstrumentAsync(
        HttpClient client,
        string adminToken,
        string prefix)
    {
        var instId = await CreateInstrumentAsync(client, adminToken, NewCode(prefix), $"Instrument {prefix}");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);

        var q1 = Guid.NewGuid();
        var opt1 = Guid.NewGuid();
        var opt1_alt = Guid.NewGuid();
        var q2 = Guid.NewGuid();
        var opt2 = Guid.NewGuid();

        var payload = Json($$"""
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

        await MarkReadyAsync(client, adminToken, instId, verId);
        await PublishAsync(client, adminToken, instId, verId);

        // Obtener los GUIDs reales generados en draft-definition consultando la BD
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var questions = (await conn.QueryAsync<dynamic>(
            "SELECT question_id, position FROM yusay.question WHERE instrument_version_id = @VerId ORDER BY position ASC",
            new { VerId = verId })).ToList();

        var actualQ1 = (Guid)questions[0].question_id;
        var actualQ2 = (Guid)questions[1].question_id;

        var q1Options = (await conn.QueryAsync<dynamic>(
            "SELECT option_id, position FROM yusay.answer_option WHERE question_id = @QId ORDER BY position ASC",
            new { QId = actualQ1 })).ToList();
        var actualOpt1 = (Guid)q1Options[0].option_id;

        var q2Options = (await conn.QueryAsync<dynamic>(
            "SELECT option_id, position FROM yusay.answer_option WHERE question_id = @QId ORDER BY position ASC",
            new { QId = actualQ2 })).ToList();
        var actualOpt2 = (Guid)q2Options[0].option_id;

        return (instId, verId, actualQ1, actualOpt1, actualQ2, actualOpt2);
    }

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
        string email = $"prog_{Guid.NewGuid():N}@yusay.local";
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
