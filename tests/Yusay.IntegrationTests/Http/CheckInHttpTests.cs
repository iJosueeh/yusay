using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
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
/// Pruebas HTTP de CheckIn (F2a de N1): creación y lectura propias, 404 uniforme para
/// recurso ajeno e inexistente (comparando los campos públicos de ProblemDetails salvo
/// traceId), RN-026 (el administrador no accede a check-ins privados ajenos), validaciones
/// de escala/ventana/mediciones y regresión del gate de autenticación.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class CheckInHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-http-integration-test-secret-0123456789abcdef-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public CheckInHttpTests(PostgreSqlFixture fixture)
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

    // ------------------------------------------------------------------------------------------
    // POST /check-ins
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Post_WithValidBody_ShouldCreateOwnedCheckInAndReturn201()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var createdBody = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var createdId = createdBody.RootElement.GetProperty("checkInId").GetGuid();
        Assert.Equal($"/check-ins/{createdId}", response.Headers.Location?.ToString());
        Assert.Equal(1, createdBody.RootElement.GetProperty("revision").GetInt32());
        Assert.NotEqual(Guid.Empty, createdId);

        // El check-in creado es consultable por su dueño con el mismo token (RN-019)
        using var client2 = factory.CreateClient();
        var getResponse = await SendAsync(client2, HttpMethod.Get, $"/check-ins/{createdId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutToken_ShouldReturn401_AndPublicEndpointsStayAnonymous()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (dimensionId, _) = await SeedDimensionAsync();

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", null, new
        {
            measurements = new[] { new { dimensionId, value = 3 } }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithGarbageToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (dimensionId, _) = await SeedDimensionAsync();

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", "garbage-token", new
        {
            measurements = new[] { new { dimensionId, value = 3 } }
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithEmptyMeasurements_ShouldReturn400ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken,
            new { measurements = Array.Empty<object>(), note = "sin mediciones" });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "al menos una Measurement");
    }

    [Fact]
    public async Task Post_WithRepeatedDimension_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[]
            {
                new { dimensionId, value = 2 },
                new { dimensionId, value = 4 }
            }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "más de una Measurement de la Dimensión");
    }

    [Theory]
    [InlineData(0, 1, 5)]   // bajo el mínimo
    [InlineData(6, 1, 5)]   // sobre el máximo
    public async Task Post_WithValueOutsideScale_ShouldReturn400(int value, int minValue, int maxValue)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync(minValue, maxValue, 1);

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[] { new { dimensionId, value } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "fuera de la escala");
    }

    [Fact]
    public async Task Post_WithStepMisalignedValue_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync(1, 9, 2); // válidos: 1,3,5,7,9

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "no es alcanzable con paso");
    }

    [Fact]
    public async Task Post_WithRecordedAtOutsideWindow_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            recordedAt = DateTimeOffset.UtcNow.AddHours(-200),
            measurements = new[] { new { dimensionId, value = 3 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "168 horas");
    }

    [Fact]
    public async Task Post_WithUnknownDimension_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[] { new { dimensionId = Guid.NewGuid(), value = 3 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "No existe ninguna Dimensión");
    }

    [Fact]
    public async Task Post_WithDimensionWithoutActiveVersion_ShouldReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync(activate: false);

        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[] { new { dimensionId, value = 3 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "no tiene una versión de escala activa");
    }

    [Fact]
    public async Task Post_WithEmptyNote_ShouldPersistNullNote()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();

        var created = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            note = "   ",
            measurements = new[] { new { dimensionId, value = 3 } }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var checkInId = (await ReadGuidAsync(created, "checkInId"));

        using var client2 = factory.CreateClient();
        var getResponse = await SendAsync(client2, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);
        using var body = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("note").ValueKind);
    }

    // ------------------------------------------------------------------------------------------
    // GET /check-ins/{id}: 404 uniforme, RN-026
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task GetById_WithOwnerToken_ShouldReturn200WithMeasurements()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, versionId) = await SeedDimensionAsync(0, 10, 1);
        var created = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            note = "Contexto personal",
            measurements = new[] { new { dimensionId, value = 7 } }
        });
        var checkInId = await ReadGuidAsync(created, "checkInId");

        using var client2 = factory.CreateClient();
        var response = await SendAsync(client2, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(checkInId, body.RootElement.GetProperty("checkInId").GetGuid());
        Assert.Equal("Contexto personal", body.RootElement.GetProperty("note").GetString());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("updatedAt").ValueKind);
        Assert.Equal(1, body.RootElement.GetProperty("revision").GetInt32());
        var measurement = Assert.Single(body.RootElement.GetProperty("measurements").EnumerateArray());
        Assert.Equal(dimensionId, measurement.GetProperty("dimensionId").GetGuid());
        Assert.Equal(versionId, measurement.GetProperty("dimensionVersionId").GetGuid());
        Assert.Equal(7, measurement.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task GetById_ForeignAndNonexistent_ShouldReturnUniform404WithIdenticalPublicFields()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, tokenA) = await CreateSessionAsync(client);
        var (_, tokenB) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var created = await SendAsync(client, HttpMethod.Post, "/check-ins", tokenA, new
        {
            measurements = new[] { new { dimensionId, value = 3 } }
        });
        var ownedId = await ReadGuidAsync(created, "checkInId");

        using var clientB = factory.CreateClient();
        var foreignResponse = await SendAsync(clientB, HttpMethod.Get, $"/check-ins/{ownedId}", tokenB);
        using var clientA2 = factory.CreateClient();
        var nonexistentResponse = await SendAsync(clientA2, HttpMethod.Get,
            $"/check-ins/{Guid.NewGuid()}", tokenA);

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);

        // 404 uniforme: los campos públicos de ProblemDetails son idénticos salvo traceId
        using var foreign = JsonDocument.Parse(await foreignResponse.Content.ReadAsStringAsync());
        using var nonexistent = JsonDocument.Parse(await nonexistentResponse.Content.ReadAsStringAsync());
        foreach (var field in new[] { "title", "detail", "status", "type" })
        {
            Assert.True(foreign.RootElement.TryGetProperty(field, out var foreignValue),
                $"Falta el campo público {field} en el 404 de recurso ajeno.");
            Assert.True(nonexistent.RootElement.TryGetProperty(field, out var nonexistentValue),
                $"Falta el campo público {field} en el 404 de recurso inexistente.");
            Assert.Equal(foreignValue.GetRawText(), nonexistentValue.GetRawText());
        }
        Assert.NotEqual(
            foreign.RootElement.GetProperty("traceId").GetString(),
            nonexistent.RootElement.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task GetById_WithAdministratorToken_ShouldReturn404ForForeignPrivateCheckIn_RN026()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync(client);
        var (adminUserId, adminToken) = await CreateSessionAsync(client);
        await EnableAdministratorAsync(adminUserId);
        var (dimensionId, _) = await SeedDimensionAsync();
        var created = await SendAsync(client, HttpMethod.Post, "/check-ins", userToken, new
        {
            measurements = new[] { new { dimensionId, value = 3 } }
        });
        var ownedId = await ReadGuidAsync(created, "checkInId");

        using var adminClient = factory.CreateClient();
        var response = await SendAsync(adminClient, HttpMethod.Get, $"/check-ins/{ownedId}", adminToken);

        // RN-026: Administrator no obtiene automáticamente acceso a check-ins privados
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WithRevokedToken_ShouldReturn401_Regression()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var created = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[] { new { dimensionId, value = 3 } }
        });
        var checkInId = await ReadGuidAsync(created, "checkInId");

        await RevokeAccessTokenAsync(client, accessToken);

        using var client2 = factory.CreateClient();
        var response = await SendAsync(client2, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "no es válido, ha expirado o ha sido revocado");
    }

    // ------------------------------------------------------------------------------------------
    // Composición
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

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync(HttpClient client)
    {
        string email = $"checkin_http_{Guid.NewGuid():N}@yusay.local";
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

    private async Task<(Guid DimensionId, Guid VersionId)> SeedDimensionAsync(
        int minValue = 1, int maxValue = 5, int step = 1, bool activate = true)
    {
        var dimensionId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension (dimension_id, code, name, description)
            VALUES (@DimensionId, 'dim_' || replace(@DimensionId::text, '-', ''), 'Dimensión de prueba', 'Escala de prueba.');
            """, new { DimensionId = dimensionId }));

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO yusay.dimension_version
                (dimension_version_id, dimension_id, version, definition, min_value, max_value, step, status)
            VALUES (@VersionId, @DimensionId, 1, 'Versión de prueba.', @MinValue, @MaxValue, @Step, @Status);
            """, new
            {
                VersionId = versionId,
                DimensionId = dimensionId,
                MinValue = minValue,
                MaxValue = maxValue,
                Step = step,
                Status = activate ? "ACTIVE" : "DRAFT"
            }));

        return (dimensionId, versionId);
    }

    private async Task EnableAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId);",
            new { UserId = userId }));
    }

    // La revocación usa el flujo real: SignOut denylista el jti en Redis (patrón Bearer)
    private async Task RevokeAccessTokenAsync(HttpClient client, string accessToken)
    {
        var signOut = new HttpRequestMessage(HttpMethod.Post, "/auth/sign-out");
        signOut.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(signOut);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<Guid> ReadGuidAsync(HttpResponseMessage response, string property)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty(property).GetGuid();
    }

    private Task<HttpResponseMessage> SendAsync(
        HttpClient client, HttpMethod method, string path, string? accessToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }
        return client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(HttpResponseMessage response, HttpStatusCode status, string detailFragment)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal((int)status, body.RootElement.GetProperty("status").GetInt32());
        Assert.Contains(detailFragment, body.RootElement.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.True(body.RootElement.TryGetProperty("traceId", out _), "ProblemDetails debe incluir traceId.");
    }
}
