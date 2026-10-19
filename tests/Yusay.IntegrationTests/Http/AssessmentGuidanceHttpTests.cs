using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Yusay.Api.Contracts.Assessment;
using Yusay.Application.Assessment.Common;
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
public sealed class AssessmentGuidanceHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret = "yusay-guidance-integration-test-secret-0123456789";
    private const string ValidPassword = "NormativePassword#2026";
    private const string MissingBearerMessage = "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AssessmentGuidanceHttpTests(PostgreSqlFixture fixture)
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
    public async Task GetGuidance_WithoutBearerToken_Returns401Unauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/assessments/{Guid.NewGuid()}/guidance");
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, MissingBearerMessage);
    }

    [Fact]
    public async Task GetGuidance_WithNonExistentAttempt_Returns404NotFound()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{Guid.NewGuid()}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "no existe");
    }

    [Fact]
    public async Task GetGuidance_WithAttemptBelongingToAnotherUser_Returns404NotFound()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, user1Token) = await CreateSessionAsync();
        var (_, user2Token) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_iso");
        var attemptId = await StartAndSubmitAttemptAsync(client, user1Token, info);

        // User2 intenta consultar la orientación de User1
        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user2Token);

        var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "no existe");
    }

    [Fact]
    public async Task GetGuidance_WithAttemptInProgress_Returns404NotFound()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_inp");

        // Start attempt pero no hacer submit
        var startResp = await SendJsonAsync(client, HttpMethod.Post, $"/instruments/{info.InstrumentId}/attempts", Json("{}"), userToken);
        startResp.EnsureSuccessStatusCode();
        var startJson = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = startJson.GetProperty("attemptId").GetGuid();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.SendAsync(request);
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "no existe");
    }

    [Fact]
    public async Task GetGuidance_WithSubmittedAttempt_Returns200WithFullGuidanceAndDisclaimer()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_full");
        var attemptId = await StartAndSubmitAttemptAsync(client, userToken, info);

        // Obtener el interpretation_id generado para el submit
        var interpretationId = await GetAttemptInterpretationIdAsync(attemptId);

        // Asociar un Topic a la Interpretation
        var topicId = await CreateTopicDirectAsync($"T_{Guid.NewGuid():N}"[..20], "Tema de orientación");
        await LinkInterpretationTopicAsync(interpretationId, topicId);

        // Crear y publicar un Resource para ese Topic
        var createResResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Artículo Educativo de Guía","summary":"Resumen educativo","body":"Contenido formativo","topicIds":["{{topicId}}"]}"""), adminToken);
        createResResp.EnsureSuccessStatusCode();
        var resourceId = (await createResResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resourceId").GetGuid();

        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resourceId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // Consultar Guidance
        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var guidance = await response.Content.ReadFromJsonAsync<AssessmentGuidanceResponse>();
        Assert.NotNull(guidance);
        Assert.Equal(attemptId, guidance.AttemptId);
        Assert.Equal(info.InstrumentId, guidance.Instrument.InstrumentId);
        Assert.Equal("Elevado", guidance.Interpretation.Label);
        Assert.Equal("Rango elevated", guidance.Interpretation.Description);
        Assert.Equal("Sin limitaciones psicométricas", guidance.Interpretation.Limitations);
        Assert.Equal(2, guidance.Result.Score);
        Assert.Equal(AssessmentGuidanceConstants.NonClinicalDisclaimer, guidance.Disclaimer);

        Assert.Single(guidance.Topics);
        Assert.Equal(topicId, guidance.Topics[0].TopicId);

        Assert.Single(guidance.Resources);
        Assert.Equal(resourceId, guidance.Resources[0].ResourceId);
        Assert.Equal("Artículo Educativo de Guía", guidance.Resources[0].Title);
        Assert.Equal("Resumen educativo", guidance.Resources[0].Summary);
    }

    [Fact]
    public async Task GetGuidance_WhenNoResourcesExist_Returns200WithEmptyResources()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_empty");
        var attemptId = await StartAndSubmitAttemptAsync(client, userToken, info);

        // Guidance sin temas ni recursos asociados a la interpretación
        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var guidance = await response.Content.ReadFromJsonAsync<AssessmentGuidanceResponse>();
        Assert.NotNull(guidance);
        Assert.Equal(attemptId, guidance.AttemptId);
        Assert.Empty(guidance.Resources);
        Assert.Equal(AssessmentGuidanceConstants.NonClinicalDisclaimer, guidance.Disclaimer);
    }

    [Fact]
    public async Task GetGuidance_ExcludesDraftAndRetiredResources()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_filt");
        var attemptId = await StartAndSubmitAttemptAsync(client, userToken, info);
        var interpretationId = await GetAttemptInterpretationIdAsync(attemptId);

        var topicId = await CreateTopicDirectAsync($"T_{Guid.NewGuid():N}"[..20], "Tema Filtro Estados");
        await LinkInterpretationTopicAsync(interpretationId, topicId);

        // 1. Recurso en DRAFT
        var draftResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Recurso en Borrador","summary":"Borrador","body":"Cuerpo","topicIds":["{{topicId}}"]}"""), adminToken);
        draftResp.EnsureSuccessStatusCode();

        // 2. Recurso que se publica y luego se retira (RETIRED)
        var retireResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Recurso Retirado","summary":"Retirado","body":"Cuerpo","topicIds":["{{topicId}}"]}"""), adminToken);
        retireResp.EnsureSuccessStatusCode();
        var retiredResId = (await retireResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resourceId").GetGuid();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{retiredResId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{retiredResId}/retire", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // 3. Recurso PUBLISHED
        var pubResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Recurso Visible Publicado","summary":"Visible","body":"Cuerpo","topicIds":["{{topicId}}"]}"""), adminToken);
        pubResp.EnsureSuccessStatusCode();
        var publishedResId = (await pubResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resourceId").GetGuid();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{publishedResId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // Consultar Guidance
        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var guidance = await response.Content.ReadFromJsonAsync<AssessmentGuidanceResponse>();
        Assert.NotNull(guidance);
        Assert.Single(guidance.Resources);
        Assert.Equal(publishedResId, guidance.Resources[0].ResourceId);
        Assert.Equal("Recurso Visible Publicado", guidance.Resources[0].Title);
    }

    [Fact]
    public async Task GetGuidance_WhenResourceIsRetired_ImmediatelyDisappearsFromGuidance()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_ret");
        var attemptId = await StartAndSubmitAttemptAsync(client, userToken, info);
        var interpretationId = await GetAttemptInterpretationIdAsync(attemptId);

        var topicId = await CreateTopicDirectAsync($"T_{Guid.NewGuid():N}"[..20], "Tema Dinámico");
        await LinkInterpretationTopicAsync(interpretationId, topicId);

        var createRes = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Recurso a Retirar","summary":"Se retirará","body":"Cuerpo","topicIds":["{{topicId}}"]}"""), adminToken);
        createRes.EnsureSuccessStatusCode();
        var resId = (await createRes.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resourceId").GetGuid();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // 1. Debe figurar inicialmente
        var req1 = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        req1.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var res1 = await client.SendAsync(req1);
        var g1 = await res1.Content.ReadFromJsonAsync<AssessmentGuidanceResponse>();
        Assert.Single(g1!.Resources);

        // 2. Admin retira el recurso
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{resId}/retire", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        // 3. Ya no figura en la siguiente consulta
        var req2 = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        req2.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);
        var res2 = await client.SendAsync(req2);
        var g2 = await res2.Content.ReadFromJsonAsync<AssessmentGuidanceResponse>();
        Assert.Empty(g2!.Resources);
    }

    [Fact]
    public async Task GetGuidance_LimitsResourcesTo10AndOrdersByTitleAndResourceId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_limit");
        var attemptId = await StartAndSubmitAttemptAsync(client, userToken, info);
        var interpretationId = await GetAttemptInterpretationIdAsync(attemptId);

        var topicId = await CreateTopicDirectAsync($"T_{Guid.NewGuid():N}"[..20], "Tema Múltiples");
        await LinkInterpretationTopicAsync(interpretationId, topicId);

        // Crear 12 recursos publicados
        for (int i = 1; i <= 12; i++)
        {
            var letter = (char)('A' + i - 1);
            var title = $"Articulo {letter:00}";
            var cResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
                Json($$"""{"type":"ARTICLE","title":"{{title}}","summary":"Sum","body":"Body","topicIds":["{{topicId}}"]}"""), adminToken);
            cResp.EnsureSuccessStatusCode();
            var rId = (await cResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resourceId").GetGuid();
            (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{rId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();
        }

        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var guidance = await response.Content.ReadFromJsonAsync<AssessmentGuidanceResponse>();
        Assert.NotNull(guidance);
        Assert.Equal(10, guidance.Resources.Count);

        // Verificar orden title ASC
        for (int i = 0; i < guidance.Resources.Count - 1; i++)
        {
            var current = guidance.Resources[i];
            var next = guidance.Resources[i + 1];
            Assert.True(string.Compare(current.Title, next.Title, StringComparison.Ordinal) <= 0);
        }
    }

    [Fact]
    public async Task GetGuidance_WithMultipleTopicsMatchingSameResource_DeduplicatesResources()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var adminToken = await CreateSessionAsyncAndGrant();
        var (_, userToken) = await CreateSessionAsync();

        var info = await CreateAndPublishInstrumentAsync(client, adminToken, "g_dedup");
        var attemptId = await StartAndSubmitAttemptAsync(client, userToken, info);
        var interpretationId = await GetAttemptInterpretationIdAsync(attemptId);

        var topic1Id = await CreateTopicDirectAsync($"T1_{Guid.NewGuid():N}"[..20], "Tema Uno");
        var topic2Id = await CreateTopicDirectAsync($"T2_{Guid.NewGuid():N}"[..20], "Tema Dos");
        await LinkInterpretationTopicAsync(interpretationId, topic1Id);
        await LinkInterpretationTopicAsync(interpretationId, topic2Id);

        // Crear un recurso con ambos temas
        var cResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/resources",
            Json($$"""{"type":"ARTICLE","title":"Recurso Compartido MultiTema","summary":"Sum","body":"Body","topicIds":["{{topic1Id}}","{{topic2Id}}"]}"""), adminToken);
        cResp.EnsureSuccessStatusCode();
        var rId = (await cResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("resourceId").GetGuid();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/resources/{rId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

        var request = new HttpRequestMessage(HttpMethod.Get, $"/assessments/{attemptId}/guidance");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", userToken);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var guidance = await response.Content.ReadFromJsonAsync<AssessmentGuidanceResponse>();
        Assert.NotNull(guidance);
        Assert.Equal(2, guidance.Topics.Count);
        Assert.Single(guidance.Resources); // Deduplicado a 1
        Assert.Equal(rId, guidance.Resources[0].ResourceId);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
        });

    private static StringContent Json(string payload) =>
        new(payload, Encoding.UTF8, "application/json");

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string url,
        HttpContent content,
        string? bearerToken = null)
    {
        var request = new HttpRequestMessage(method, url) { Content = content };
        if (!string.IsNullOrEmpty(bearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }
        return await client.SendAsync(request);
    }

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync()
    {
        var email = $"guidance_{Guid.NewGuid():N}@yusay.local";
        var registration = await new RegisterUserUseCase(
                _unitOfWork, _userAccountRepo, _userCredentialRepo, _verificationTokenRepo,
                _auditEventRepo, _passwordHasher, new SecureTokenService(),
                new NullEmailVerificationSender())
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
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync("INSERT INTO yusay.administrator (user_id) VALUES (@UserId)", new { UserId = userId });
        return accessToken;
    }

    private async Task<Guid> CreateTopicDirectAsync(string code, string name)
    {
        var id = Guid.NewGuid();
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO yusay.topic (topic_id, code, name, description) VALUES (@Id, @Code, @Name, 'Topic test desc')",
            new { Id = id, Code = code, Name = name });
        return id;
    }

    private async Task LinkInterpretationTopicAsync(Guid interpretationId, Guid topicId)
    {
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO yusay.interpretation_topic (interpretation_id, topic_id) VALUES (@InterpretationId, @TopicId)",
            new { InterpretationId = interpretationId, TopicId = topicId });
    }

    private async Task<Guid> GetAttemptInterpretationIdAsync(Guid attemptId)
    {
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await conn.QuerySingleAsync<Guid>(
            "SELECT interpretation_id FROM yusay.assessment_result WHERE attempt_id = @AttemptId",
            new { AttemptId = attemptId });
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedSubstring)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        var detail = problem.Detail ?? problem.Title ?? string.Empty;
        Assert.Contains(expectedSubstring, detail, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record InstrumentInfo(
        Guid InstrumentId,
        Guid VersionId,
        Guid Q1,
        Guid Opt1_1,
        Guid Opt1_2,
        Guid Q2,
        Guid Opt2_1,
        Guid Opt2_2);

    private async Task<InstrumentInfo> CreateAndPublishInstrumentAsync(
        HttpClient client,
        string adminToken,
        string prefix)
    {
        var instResp = await SendJsonAsync(client, HttpMethod.Post, "/admin/instruments",
            JsonContent.Create(new
            {
                code = $"{prefix}_{Guid.NewGuid():N}"[..20],
                name = $"Instrument {prefix}",
                description = "General desc",
                purpose = "Psychometric purpose"
            }), adminToken);
        instResp.EnsureSuccessStatusCode();
        var instId = (await instResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instrumentId").GetGuid();

        var verResp = await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instId}/versions",
            JsonContent.Create(new
            {
                version = 1,
                sourceDescription = "Fuente",
                population = "Población",
                administrationConditions = "Condiciones",
                licenseInformation = "CC-BY",
                limitations = "Sin limitaciones"
            }), adminToken);
        verResp.EnsureSuccessStatusCode();
        var verId = (await verResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("instrumentVersionId").GetGuid();

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
                { "label": "Normal", "description": "Rango normal", "limitations": "Sin limitaciones", "lowerBound": 0, "upperBound": 1 },
                { "label": "Elevado", "description": "Rango elevated", "limitations": "Sin limitaciones psicométricas", "lowerBound": 2, "upperBound": 2 }
            ]
        }
        """);

        (await SendJsonAsync(client, HttpMethod.Put, $"/admin/instruments/{instId}/versions/{verId}/draft-definition", payload, adminToken)).EnsureSuccessStatusCode();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instId}/versions/{verId}/mark-ready", Json("{}"), adminToken)).EnsureSuccessStatusCode();
        (await SendJsonAsync(client, HttpMethod.Post, $"/admin/instruments/{instId}/versions/{verId}/publish", Json("{}"), adminToken)).EnsureSuccessStatusCode();

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

        return new InstrumentInfo(instId, verId, actualQ1, actualOpt1_1, actualOpt1_2, actualQ2, actualOpt2_1, actualOpt2_2);
    }

    private static async Task<Guid> StartAndSubmitAttemptAsync(
        HttpClient client,
        string userToken,
        InstrumentInfo info)
    {
        var startResp = await SendJsonAsync(client, HttpMethod.Post, $"/instruments/{info.InstrumentId}/attempts", Json("{}"), userToken);
        startResp.EnsureSuccessStatusCode();
        var startJson = await startResp.Content.ReadFromJsonAsync<JsonElement>();
        var attemptId = startJson.GetProperty("attemptId").GetGuid();

        // Responder con score 2 (Opción 1B [contrib=1] + Opción 2B [contrib=1])
        var saveAnswersPayload = JsonContent.Create(new
        {
            answers = new[]
            {
                new { questionId = info.Q1, optionId = info.Opt1_2 },
                new { questionId = info.Q2, optionId = info.Opt2_2 }
            }
        });
        (await SendJsonAsync(client, HttpMethod.Put, $"/assessments/{attemptId}/answers", saveAnswersPayload, userToken)).EnsureSuccessStatusCode();

        // Submit
        (await SendJsonAsync(client, HttpMethod.Post, $"/assessments/{attemptId}/submit", Json("{}"), userToken)).EnsureSuccessStatusCode();

        return attemptId;
    }
}
