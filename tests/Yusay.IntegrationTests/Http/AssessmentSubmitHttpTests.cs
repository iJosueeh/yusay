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
public sealed class AssessmentSubmitHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-assessment-submit-http-integration-test-secret-0123456789";

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

    public AssessmentSubmitHttpTests(PostgreSqlFixture fixture)
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
    public async Task Submit_WithoutToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var attemptId = Guid.NewGuid();
        var resp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", null);
        await AssertProblemAsync(resp, HttpStatusCode.Unauthorized, "No autorizado", MissingBearerMessage);
    }

    [Fact]
    public async Task Submit_WhenAttemptNotFoundOrBelongsToAnotherUser_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken1) = await CreateSessionAsync();
        var (_, userToken2) = await CreateSessionAsync();

        var (instrumentId, _, _, _, _, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "sub_priv");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken1);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // User2 intenta enviar el intento de User1 -> 404
        var submitResp2 = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken2);
        await AssertProblemAsync(submitResp2, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El intento de evaluación solicitado no existe.");

        // User1 intenta enviar un intento inexistente -> 404
        var nonExistentId = Guid.NewGuid();
        var submitNonExistent = await SendPostAsync(client, $"/assessments/{nonExistentId}/submit", userToken1);
        await AssertProblemAsync(submitNonExistent, HttpStatusCode.NotFound, "Recurso no encontrado",
            "El intento de evaluación solicitado no existe.");
    }

    [Fact]
    public async Task Submit_WhenIncompleteAnswers_ShouldReturn400AndRollback()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1, _, _) = await CreateAndPublishInstrumentAsync(client, adminToken, "sub_incomp");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder solo Q1 (falta Q2 que es obligatoria)
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Intentar submit -> 400 Bad Request
        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        await AssertProblemAsync(submitResp, HttpStatusCode.BadRequest, "Solicitud no válida",
            "No se puede enviar la evaluación: faltan respuestas a preguntas obligatorias.");

        // Verificar que el intento continúa IN_PROGRESS y no se creó assessment_result
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var attemptRow = await conn.QuerySingleAsync<dynamic>(
            "SELECT status, ended_at FROM yusay.assessment_attempt WHERE attempt_id = @AttemptId",
            new { AttemptId = attemptId });
        Assert.Equal("IN_PROGRESS", (string)attemptRow.status);
        Assert.Null(attemptRow.ended_at);

        int resultCount = await conn.QuerySingleAsync<int>(
            "SELECT count(*) FROM yusay.assessment_result WHERE attempt_id = @AttemptId",
            new { AttemptId = attemptId });
        Assert.Equal(0, resultCount);
    }

    [Fact]
    public async Task Submit_WhenCompleteAndValid_ShouldSucceedAndPersistResult()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, versionId, q1, opt1_1, opt1_2, q2, opt2_1, opt2_2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "sub_valid");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder Q1 con opt1_2 (contrib 1) y Q2 con opt2_2 (contrib 1) -> Total Score = 2
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

        // Submit exitoso
        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        using var submitDoc = JsonDocument.Parse(await submitResp.Content.ReadAsStringAsync());
        Assert.Equal(attemptId, submitDoc.RootElement.GetProperty("attemptId").GetGuid());
        Assert.Equal(instrumentId, submitDoc.RootElement.GetProperty("instrumentId").GetGuid());
        Assert.Equal(versionId, submitDoc.RootElement.GetProperty("instrumentVersionId").GetGuid());
        Assert.Equal(1, submitDoc.RootElement.GetProperty("version").GetInt32());
        Assert.Equal("SUBMITTED", submitDoc.RootElement.GetProperty("status").GetString());
        Assert.Equal(2, submitDoc.RootElement.GetProperty("score").GetInt32());

        var interp = submitDoc.RootElement.GetProperty("interpretation");
        Assert.Equal("Elevado", interp.GetProperty("label").GetString());
        Assert.False(string.IsNullOrWhiteSpace(interp.GetProperty("description").GetString()));

        // Verificar persistencia en base de datos
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            var attemptRow = await conn.QuerySingleAsync<dynamic>(
                "SELECT status, ended_at FROM yusay.assessment_attempt WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });
            Assert.Equal("SUBMITTED", (string)attemptRow.status);
            Assert.NotNull(attemptRow.ended_at);

            var resultRow = await conn.QuerySingleAsync<dynamic>(
                "SELECT score, interpretation_id, calculated_at FROM yusay.assessment_result WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });
            Assert.Equal(2, (int)resultRow.score);
            Assert.NotNull(resultRow.calculated_at);

            // Las respuestas se preservan intactas
            int answersCount = await conn.QuerySingleAsync<int>(
                "SELECT count(*) FROM yusay.answer WHERE attempt_id = @AttemptId",
                new { AttemptId = attemptId });
            Assert.Equal(2, answersCount);
        }
    }

    [Fact]
    public async Task Submit_Idempotency_ShouldReturnExistingResultWithoutRecalculation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1_1, _, q2, opt2_1, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "sub_idemp");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder Q1=opt1_1 (contrib 0) y Q2=opt2_1 (contrib 0) -> Total Score = 0 ("Normal")
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_1}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // 1. Primer Submit
        var submit1Resp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submit1Resp.StatusCode);
        using var submit1Doc = JsonDocument.Parse(await submit1Resp.Content.ReadAsStringAsync());
        var endedAt1 = submit1Doc.RootElement.GetProperty("endedAt").GetString();
        Assert.Equal(0, submit1Doc.RootElement.GetProperty("score").GetInt32());
        Assert.Equal("Normal", submit1Doc.RootElement.GetProperty("interpretation").GetProperty("label").GetString());

        // 2. Segundo Submit (Idempotencia)
        var submit2Resp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submit2Resp.StatusCode);
        using var submit2Doc = JsonDocument.Parse(await submit2Resp.Content.ReadAsStringAsync());
        var endedAt2 = submit2Doc.RootElement.GetProperty("endedAt").GetString();
        Assert.NotNull(endedAt1);
        Assert.NotNull(endedAt2);
        Assert.Equal(DateTimeOffset.Parse(endedAt1!), DateTimeOffset.Parse(endedAt2!), TimeSpan.FromMilliseconds(1));
        Assert.Equal(0, submit2Doc.RootElement.GetProperty("score").GetInt32());
        Assert.Equal("Normal", submit2Doc.RootElement.GetProperty("interpretation").GetProperty("label").GetString());

        // Verificar que sigue existiendo exactamente un resultado oficial en BD
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        int resultCount = await conn.QuerySingleAsync<int>(
            "SELECT count(*) FROM yusay.assessment_result WHERE attempt_id = @AttemptId",
            new { AttemptId = attemptId });
        Assert.Equal(1, resultCount);
    }

    [Fact]
    public async Task Submit_PostSubmitImmutability_PutAnswersShouldReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1_1, _, q2, opt2_1, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "sub_immut");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder ambas y enviar
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_1}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // Intentar modificar respuestas tras submit -> 409 Conflict
        var postSubmitPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_1}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var postSubmitPut = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", postSubmitPayload, userToken);
        await AssertProblemAsync(postSubmitPut, HttpStatusCode.Conflict, "Conflicto",
            "El intento de evaluación ya no se encuentra en curso.");
    }

    [Fact]
    public async Task Submit_LazyExpiration_WhenExpired_ShouldCommitExpirationAndReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1_1, _, q2, opt2_1, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "sub_lazyp");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder ambas
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_1}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Envejecer intento
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

        // Submit debe devolver 409 Conflict
        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        await AssertProblemAsync(submitResp, HttpStatusCode.Conflict, "Conflicto",
            "El intento de evaluación ha expirado y no admite más respuestas.");

        // Verificar en BD: EXPIRED, ended_at poblado, y respuestas eliminadas
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
    public async Task Submit_Continuity_WhenVersionRetired_SubmitShouldSucceed()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, versionId, q1, opt1_1, _, q2, opt2_1, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "sub_ret");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder
        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_1}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        // Administrador retira la versión del instrumento
        await RetireAsync(client, adminToken, instrumentId, versionId);

        // El usuario puede enviar el intento iniciado previamente con éxito
        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);
        using var submitDoc = JsonDocument.Parse(await submitResp.Content.ReadAsStringAsync());
        Assert.Equal("SUBMITTED", submitDoc.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Submit_Concurrency_SimultaneousSubmitAndPut_ShouldSerializeCleanly()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1_1, opt1_2, q2, opt2_1, opt2_2) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "sub_conc");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        // Responder inicialmente
        var initialPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_1}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var initPut = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", initialPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, initPut.StatusCode);

        // Disparar simultáneamente submit y actualización de respuestas
        var submitTask = Task.Run(async () =>
        {
            using var localClient = factory.CreateClient();
            return await SendPostAsync(localClient, $"/assessments/{attemptId}/submit", userToken);
        });

        var putTask = Task.Run(async () =>
        {
            using var localClient = factory.CreateClient();
            var updatePayload = Json($$"""
            {
                "answers": [
                    { "questionId": "{{q1}}", "optionId": "{{opt1_2}}" },
                    { "questionId": "{{q2}}", "optionId": "{{opt2_2}}" }
                ]
            }
            """);
            return await SendJsonAsync(localClient, HttpMethod.Put, $"/assessments/{attemptId}/answers", updatePayload, userToken);
        });

        await Task.WhenAll(submitTask, putTask);

        var submitResp = await submitTask;
        var putResp = await putTask;

        // Submit siempre debe ser exitoso (200 OK)
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

        // El PUT puede haber completado antes del submit (200 OK) o después (409 Conflict)
        Assert.True(putResp.StatusCode == HttpStatusCode.OK || putResp.StatusCode == HttpStatusCode.Conflict);

        // El estado final en base de datos debe ser exactamente SUBMITTED con 1 resultado
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var attemptStatus = await conn.QuerySingleAsync<string>(
            "SELECT status FROM yusay.assessment_attempt WHERE attempt_id = @AttemptId",
            new { AttemptId = attemptId });
        Assert.Equal("SUBMITTED", attemptStatus);
    }

    [Fact]
    public async Task Submit_Privacy_ShouldNotProduceAuditEvents()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var (adminId, adminToken) = await CreateSessionAsync();
        await GrantAdministratorAsync(adminId);

        var (_, userToken) = await CreateSessionAsync();

        var (instrumentId, _, q1, opt1_1, _, q2, opt2_1, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, "sub_audit");

        var startResp = await SendPostAsync(client, $"/instruments/{instrumentId}/attempts", userToken);
        startResp.EnsureSuccessStatusCode();
        using var startDoc = JsonDocument.Parse(await startResp.Content.ReadAsStringAsync());
        var attemptId = startDoc.RootElement.GetProperty("attemptId").GetGuid();

        var putPayload = Json($$"""
        {
            "answers": [
                { "questionId": "{{q1}}", "optionId": "{{opt1_1}}" },
                { "questionId": "{{q2}}", "optionId": "{{opt2_1}}" }
            ]
        }
        """);
        var putResp = await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", putPayload, userToken);
        Assert.Equal(HttpStatusCode.OK, putResp.StatusCode);

        int auditCountBefore = await GetAuditEventCountAsync();

        // Enviar evaluación
        var submitResp = await SendPostAsync(client, $"/assessments/{attemptId}/submit", userToken);
        Assert.Equal(HttpStatusCode.OK, submitResp.StatusCode);

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
        var (instId, verId, q1, opt1, _, q2, opt2, _) =
            await CreateAndPublishInstrumentWithFullOptionsAsync(client, adminToken, prefix);
        return (instId, verId, q1, opt1, q2, opt2);
    }

    private async Task<(Guid InstrumentId, Guid VersionId, Guid Q1, Guid Opt1_1, Guid Opt1_2, Guid Q2, Guid Opt2_1, Guid Opt2_2)>
        CreateAndPublishInstrumentWithFullOptionsAsync(
            HttpClient client,
            string adminToken,
            string prefix)
    {
        var instId = await CreateInstrumentAsync(client, adminToken, NewCode(prefix), $"Instrument {prefix}");
        var verId = await CreateVersionAsync(client, adminToken, instId, 1);

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

    private static StringContent Json(string value) =>
        new(value, Encoding.UTF8, "application/json");

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
        string email = $"sub_{Guid.NewGuid():N}@yusay.local";
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
