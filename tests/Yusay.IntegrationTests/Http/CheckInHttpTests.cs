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
/// Pruebas HTTP de CheckIn (F2a de N1 + F2b-1 + F2b-2): creación y lectura propias,
/// edición optimista con ventana absoluta de 168 horas, eliminación física sin ventana
/// con revisión en el cuerpo, 404 uniforme para recurso ajeno e inexistente (comparando
/// los campos públicos de ProblemDetails salvo traceId), RN-026 (el administrador no
/// accede a check-ins privados ajenos), validaciones de escala/ventana/mediciones,
/// carreras de concurrencia optimista y regresión del gate de autenticación.
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
    // PUT /check-ins/{id} (F2b-1): OQ-DOM-008 / RN-022 / MP-PHYS-007
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Put_WithValidBody_ShouldUpdateOwnedCheckInAndReturn200Representation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, versionId) = await SeedDimensionAsync(0, 10, 1);
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        using var before = await GetRepresentationAsync(factory, checkInId, accessToken);
        var createdAt = before.RootElement.GetProperty("createdAt").GetDateTimeOffset();
        var originalRecordedAt = before.RootElement.GetProperty("recordedAt").GetDateTimeOffset();
        Assert.Equal(1, before.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal(JsonValueKind.Null, before.RootElement.GetProperty("updatedAt").ValueKind);

        var correctedRecordedAt = originalRecordedAt.AddHours(-2);
        using var putClient = factory.CreateClient();
        var response = await SendAsync(putClient, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt = correctedRecordedAt,
            note = "Corregido",
            measurements = new[] { new { dimensionId, value = 7 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(checkInId, body.RootElement.GetProperty("checkInId").GetGuid());
        Assert.Equal(2, body.RootElement.GetProperty("revision").GetInt32()); // incremento exacto de uno
        Assert.NotEqual(JsonValueKind.Null, body.RootElement.GetProperty("updatedAt").ValueKind);
        Assert.Equal("Corregido", body.RootElement.GetProperty("note").GetString());
        Assert.Equal(correctedRecordedAt, body.RootElement.GetProperty("recordedAt").GetDateTimeOffset());
        Assert.Equal(createdAt, body.RootElement.GetProperty("createdAt").GetDateTimeOffset()); // inmutable
        var measurement = Assert.Single(body.RootElement.GetProperty("measurements").EnumerateArray());
        Assert.Equal(dimensionId, measurement.GetProperty("dimensionId").GetGuid());
        Assert.Equal(versionId, measurement.GetProperty("dimensionVersionId").GetGuid()); // versión almacenada intacta
        Assert.Equal(7, measurement.GetProperty("value").GetInt32());

        // La representación posterior de GET refleja exactamente la devuelta por PUT
        using var after = await GetRepresentationAsync(factory, checkInId, accessToken);
        Assert.Equal(2, after.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal("Corregido", after.RootElement.GetProperty("note").GetString());
        Assert.Equal(correctedRecordedAt, after.RootElement.GetProperty("recordedAt").GetDateTimeOffset());
        Assert.Equal(createdAt, after.RootElement.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal(7, after.RootElement.GetProperty("measurements").EnumerateArray()
            .Single().GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Put_WithoutOrWithGarbageToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();
        var payload = new
        {
            revision = 1,
            recordedAt,
            note = "sin identidad",
            measurements = new[] { new { dimensionId, value = 4 } }
        };

        using var anonymousClient = factory.CreateClient();
        var anonymousResponse = await SendAsync(anonymousClient, HttpMethod.Put, $"/check-ins/{checkInId}", null, payload);

        using var garbageClient = factory.CreateClient();
        var garbageResponse = await SendAsync(garbageClient, HttpMethod.Put, $"/check-ins/{checkInId}", "garbage-token", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbageResponse.StatusCode);
    }

    [Fact]
    public async Task Put_WithoutRevision_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            recordedAt,
            note = "sin revisión",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "obligatoria");
    }

    [Fact]
    public async Task Put_WithoutRecordedAt_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            note = "sin instante",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "recordedAt");
    }

    [Fact]
    public async Task Put_WithoutMeasurements_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "sin mediciones"
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "al menos una Measurement");
    }

    [Fact]
    public async Task Put_ForeignWithStaleRevisionAndNonexistent_ShouldReturnUniform404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, tokenA) = await CreateSessionAsync(client);
        var (_, tokenB) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var ownedId = await CreateCheckInAsync(factory, tokenA, dimensionId);
        using var representation = await GetRepresentationAsync(factory, ownedId, tokenA);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        // Revisión también desactualizada en el recurso ajeno: aún así prima el 404 de
        // propiedad —el 409 exige verificar la propiedad antes de diagnosticar.
        using var clientB = factory.CreateClient();
        var foreignResponse = await SendAsync(clientB, HttpMethod.Put, $"/check-ins/{ownedId}", tokenB, new
        {
            revision = 999,
            recordedAt,
            note = "ajeno",
            measurements = new[] { new { dimensionId, value = 4 } }
        });
        using var clientA2 = factory.CreateClient();
        var nonexistentResponse = await SendAsync(clientA2, HttpMethod.Put,
            $"/check-ins/{Guid.NewGuid()}", tokenA, new
            {
                revision = 1,
                recordedAt,
                note = "inexistente",
                measurements = new[] { new { dimensionId, value = 4 } }
            });

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);

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
    public async Task Put_WithAdministratorToken_ShouldReturn404ForForeignPrivateCheckIn_RN026()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync(client);
        var (adminUserId, adminToken) = await CreateSessionAsync(client);
        await EnableAdministratorAsync(adminUserId);
        var (dimensionId, _) = await SeedDimensionAsync();
        var ownedId = await CreateCheckInAsync(factory, userToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, ownedId, userToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        using var adminClient = factory.CreateClient();
        var response = await SendAsync(adminClient, HttpMethod.Put, $"/check-ins/{ownedId}", adminToken, new
        {
            revision = 1,
            recordedAt,
            note = "administración",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        // RN-026: Administrator no obtiene automáticamente acceso a check-ins privados
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_WithStaleRevision_ShouldReturn409AndPreserveState()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        using var client2 = factory.CreateClient();
        var first = await SendAsync(client2, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "primera edición",
            measurements = new[] { new { dimensionId, value = 4 } }
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var client3 = factory.CreateClient();
        var second = await SendAsync(client3, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1, // la revisión vigente ya es 2
            recordedAt,
            note = "segunda edición",
            measurements = new[] { new { dimensionId, value = 5 } }
        });

        await AssertProblemAsync(second, HttpStatusCode.Conflict, "revisión vigente");

        using var after = await GetRepresentationAsync(factory, checkInId, accessToken);
        Assert.Equal(2, after.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal("primera edición", after.RootElement.GetProperty("note").GetString());
        Assert.Equal(4, after.RootElement.GetProperty("measurements").EnumerateArray()
            .Single().GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Put_TwoConcurrentRequestsWithSameRevision_ExactlyOneShouldSucceed()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var payload = new
        {
            revision = 1,
            recordedAt,
            note = "carrera",
            measurements = new[] { new { dimensionId, value = 4 } }
        };

        // Dos ediciones concurrentes con la misma revisión: exactamente una gana el
        // compromiso optimista y la sentencia condicional decide en la base de datos.
        var tasks = Enumerable.Range(0, 2)
            .Select(_ =>
            {
                var concurrentClient = factory.CreateClient();
                return SendAsync(concurrentClient, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, payload);
            })
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var statuses = responses.Select(response => response.StatusCode)
            .OrderBy(status => status)
            .ToArray();
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, statuses);

        using var after = await GetRepresentationAsync(factory, checkInId, accessToken);
        Assert.Equal(2, after.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal("carrera", after.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Put_WhenEditWindowExpired_ShouldReturn409()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        // created_at queda 169 horas en el pasado: la ventana absoluta de 168 h expiró
        await ShiftEditWindowAsync(checkInId, hours: 169);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();
        Assert.Equal(1, representation.RootElement.GetProperty("revision").GetInt32());

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "fuera de ventana",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.Conflict, "ventana");
    }

    [Fact]
    public async Task Put_WhileEditWindowRemainsOpenNearEdge_ShouldReturn200()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        // created_at a 167 horas: la ventana sigue abierta (borde inferior no alcanzado)
        await ShiftEditWindowAsync(checkInId, hours: 167);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "cerca del borde",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, body.RootElement.GetProperty("revision").GetInt32());
    }

    [Theory]
    [InlineData(1)]     // recordedAt posterior a created_at
    [InlineData(-169)]  // recordedAt anterior a created_at - 168 h
    public async Task Put_WithRecordedAtOutsidePhysicalWindow_ShouldReturn400(int hoursOffset)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var baseInstant = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt = baseInstant.AddHours(hoursOffset),
            note = "instante inválido",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "168 horas");
    }

    [Fact]
    public async Task Put_WithRecordedAtAtExactWindowEdge_ShouldReturn200()
    {
        // Intervalo físico inclusivo: recorded_at == created_at - 168 h es válido en
        // dominio y en el CHECK ck_check_in_recorded_window.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var baseInstant = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt = baseInstant.AddHours(-168),
            note = "borde físico",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(baseInstant.AddHours(-168), body.RootElement.GetProperty("recordedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Put_WithUnknownDimensionInBody_ShouldReturn400_ImmutableSet()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var (otherDimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "dimensión ajena",
            measurements = new[] { new { dimensionId = otherDimensionId, value = 3 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "inmutable");
    }

    [Fact]
    public async Task Put_WithMissingStoredDimensionInBody_ShouldReturn400_ImmutableSet()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync(0, 10, 1);
        var (otherDimensionId, _) = await SeedDimensionAsync(0, 10, 1);

        var created = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            measurements = new[]
            {
                new { dimensionId, value = 3 },
                new { dimensionId = otherDimensionId, value = 5 }
            }
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var checkInId = await ReadGuidAsync(created, "checkInId");
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "dimensión retirada",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "inmutable");
        Assert.Equal(2, representation.RootElement.GetProperty("measurements").GetArrayLength());
    }

    [Fact]
    public async Task Put_WithValueOutsideStoredScale_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync(1, 5, 1);
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "escala excedida",
            measurements = new[] { new { dimensionId, value = 7 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "fuera de la escala");
    }

    [Fact]
    public async Task Put_WithMisalignedStepValue_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync(1, 9, 2); // válidos: 1, 3, 5, 7, 9
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId, value: 3);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "paso incorrecto",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "no es alcanzable con paso");
    }

    [Fact]
    public async Task Put_WithRetiredDimensionVersion_ShouldValidateAgainstStoredScale()
    {
        // OQ-DOM-008: la edición valida contra la DimensionVersion originalmente
        // almacenada incluso cuando la versión ya está RETIRED (y V015 congeló su escala).
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, versionId) = await SeedDimensionAsync(1, 5, 1);
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId, value: 3);

        await RetireDimensionVersionAsync(versionId);
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var invalid = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "fuera de la escala almacenada",
            measurements = new[] { new { dimensionId, value = 7 } }
        });
        await AssertProblemAsync(invalid, HttpStatusCode.BadRequest, "fuera de la escala");

        var valid = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "editado con versión retirada",
            measurements = new[] { new { dimensionId, value = 4 } }
        });
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);

        using var after = await GetRepresentationAsync(factory, checkInId, accessToken);
        var measurement = Assert.Single(after.RootElement.GetProperty("measurements").EnumerateArray());
        Assert.Equal(versionId, measurement.GetProperty("dimensionVersionId").GetGuid()); // procedencia histórica
        Assert.Equal(4, measurement.GetProperty("value").GetInt32());
    }

    [Fact]
    public async Task Put_WithEmptyNote_ShouldClearNote()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId, note: "Original");
        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = representation.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var response = await SendAsync(client, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "   ",
            measurements = new[] { new { dimensionId, value = 4 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var after = await GetRepresentationAsync(factory, checkInId, accessToken);
        Assert.Equal(JsonValueKind.Null, after.RootElement.GetProperty("note").ValueKind);
    }

    // ------------------------------------------------------------------------------------------
    // DELETE /check-ins/{id} (F2b-2): OQ-DOM-009 / RN-022 / RF-011 — revisión en el cuerpo
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Delete_WithMatchingRevision_ShouldReturn204WithEmptyBody()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        var response = await SendAsync(client, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken,
            new { revision = 1 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.True(response.Content.Headers.ContentLength is null or 0,
            "El 204 No Content no debe incluir cuerpo.");
    }

    [Fact]
    public async Task Get_AfterSuccessfulDelete_ShouldReturn404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        var deleteResponse = await SendAsync(client, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken,
            new { revision = 1 });
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        using var getClient = factory.CreateClient();
        var getResponse = await SendAsync(getClient, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);
        await AssertProblemAsync(getResponse, HttpStatusCode.NotFound, "no existe");
    }

    [Fact]
    public async Task Delete_Repeatedly_ShouldReturnUniform404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        var first = await SendAsync(client, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken,
            new { revision = 1 });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        // El recurso ya eliminado es indistinguible de uno inexistente
        using var secondClient = factory.CreateClient();
        var second = await SendAsync(secondClient, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken,
            new { revision = 1 });
        await AssertProblemAsync(second, HttpStatusCode.NotFound, "El CheckIn solicitado no existe.");
    }

    [Fact]
    public async Task Delete_ForeignAndNonexistent_ShouldReturnUniform404()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, tokenA) = await CreateSessionAsync(client);
        var (_, tokenB) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var ownedId = await CreateCheckInAsync(factory, tokenA, dimensionId);

        // Ajeno con la revisión correcta e inexistente con la misma revisión: mismo 404
        using var clientB = factory.CreateClient();
        var foreignResponse = await SendAsync(clientB, HttpMethod.Delete, $"/check-ins/{ownedId}", tokenB,
            new { revision = 1 });
        using var clientA2 = factory.CreateClient();
        var nonexistentResponse = await SendAsync(clientA2, HttpMethod.Delete,
            $"/check-ins/{Guid.NewGuid()}", tokenA, new { revision = 1 });

        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, nonexistentResponse.StatusCode);

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

        // El recurso ajeno permanece intacto
        using var getA = factory.CreateClient();
        var getResponse = await SendAsync(getA, HttpMethod.Get, $"/check-ins/{ownedId}", tokenA);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_ForeignWithStaleRevision_ShouldReturn404Never409()
    {
        // La propiedad se verifica antes de diagnosticar la revisión: el recurso ajeno
        // con revisión desactualizada produce 404, jamás 409.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, tokenA) = await CreateSessionAsync(client);
        var (_, tokenB) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var ownedId = await CreateCheckInAsync(factory, tokenA, dimensionId);

        using var clientB = factory.CreateClient();
        var response = await SendAsync(clientB, HttpMethod.Delete, $"/check-ins/{ownedId}", tokenB,
            new { revision = 999 });

        await AssertProblemAsync(response, HttpStatusCode.NotFound, "El CheckIn solicitado no existe.");

        using var getA = factory.CreateClient();
        var getResponse = await SendAsync(getA, HttpMethod.Get, $"/check-ins/{ownedId}", tokenA);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WithStaleRevision_ShouldReturn409AndPreserveCheckIn()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        // Un PUT legítimo deja la revisión vigente en 2
        using var before = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = before.RootElement.GetProperty("recordedAt").GetDateTimeOffset();
        using var putClient = factory.CreateClient();
        var putResponse = await SendAsync(putClient, HttpMethod.Put, $"/check-ins/{checkInId}", accessToken, new
        {
            revision = 1,
            recordedAt,
            note = "Edición previa",
            measurements = new[] { new { dimensionId, value = 4 } }
        });
        Assert.Equal(HttpStatusCode.OK, putResponse.StatusCode);

        // DELETE con la revisión vieja: 409 solo porque el recurso es propio
        using var deleteClient = factory.CreateClient();
        var deleteResponse = await SendAsync(deleteClient, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken,
            new { revision = 1 });
        await AssertProblemAsync(deleteResponse, HttpStatusCode.Conflict, "revisión vigente");

        using var after = await GetRepresentationAsync(factory, checkInId, accessToken);
        Assert.Equal(2, after.RootElement.GetProperty("revision").GetInt32());
        Assert.Equal("Edición previa", after.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public async Task Delete_WhenCheckInOlderThan168Hours_ShouldReturn204()
    {
        // OQ-DOM-009: la eliminación no admite ventana temporal, a diferencia del PUT.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        await ShiftEditWindowAsync(checkInId, hours: 200);

        var response = await SendAsync(client, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken,
            new { revision = 1 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var getClient = factory.CreateClient();
        var getResponse = await SendAsync(getClient, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_TwoConcurrentRequestsWithSameRevision_ExactlyOneShouldSucceed()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        var payload = new { revision = 1 };
        var tasks = Enumerable.Range(0, 2)
            .Select(_ =>
            {
                var concurrentClient = factory.CreateClient();
                return SendAsync(concurrentClient, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken, payload);
            })
            .ToArray();
        var responses = await Task.WhenAll(tasks);

        var statuses = responses.Select(response => response.StatusCode)
            .OrderBy(status => status)
            .ToArray();
        Assert.Equal(new[] { HttpStatusCode.NoContent, HttpStatusCode.NotFound }, statuses);

        using var getAfter = factory.CreateClient();
        var getResponse = await SendAsync(getAfter, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_ConcurrentWithPut_ShouldRemainCoherentWithOptimisticRevision()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        using var before = await GetRepresentationAsync(factory, checkInId, accessToken);
        var recordedAt = before.RootElement.GetProperty("recordedAt").GetDateTimeOffset();

        var putPayload = new
        {
            revision = 1,
            recordedAt,
            note = "Edición concurrente",
            measurements = new[] { new { dimensionId, value = 4 } }
        };
        var deletePayload = new { revision = 1 };

        var putTask = SendAsync(factory.CreateClient(), HttpMethod.Put, $"/check-ins/{checkInId}",
            accessToken, putPayload);
        var deleteTask = SendAsync(factory.CreateClient(), HttpMethod.Delete, $"/check-ins/{checkInId}",
            accessToken, deletePayload);
        await Task.WhenAll(putTask, deleteTask);
        var putResponse = await putTask;
        var deleteResponse = await deleteTask;

        // Solo dos emparejamientos coherentes con la revisión optimista: si el PUT gana
        // (200), el DELETE queda obsoleto (409) y el recurso sobrevive con revisión 2;
        // si el DELETE gana (204), el PUT encuentra el recurso eliminado (404).
        var putWon = putResponse.StatusCode == HttpStatusCode.OK;
        var coherent =
            (putWon && deleteResponse.StatusCode == HttpStatusCode.Conflict) ||
            (!putWon &&
             putResponse.StatusCode == HttpStatusCode.NotFound &&
             deleteResponse.StatusCode == HttpStatusCode.NoContent);
        Assert.True(coherent,
            $"Combinación incoherente: PUT={putResponse.StatusCode}, DELETE={deleteResponse.StatusCode}.");

        using var getAfter = factory.CreateClient();
        var getResponse = await SendAsync(getAfter, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);
        if (putWon)
        {
            Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
            using var after = JsonDocument.Parse(await getResponse.Content.ReadAsStringAsync());
            Assert.Equal(2, after.RootElement.GetProperty("revision").GetInt32());
        }
        else
        {
            Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        }
    }

    [Fact]
    public async Task Delete_WithAdministratorToken_ShouldReturn404ForForeignPrivateCheckIn_RN026()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, userToken) = await CreateSessionAsync(client);
        var (adminUserId, adminToken) = await CreateSessionAsync(client);
        await EnableAdministratorAsync(adminUserId);
        var (dimensionId, _) = await SeedDimensionAsync();
        var ownedId = await CreateCheckInAsync(factory, userToken, dimensionId);

        using var adminClient = factory.CreateClient();
        var response = await SendAsync(adminClient, HttpMethod.Delete, $"/check-ins/{ownedId}", adminToken,
            new { revision = 1 });

        // RN-026: Administrator no obtiene automáticamente acceso a check-ins privados
        await AssertProblemAsync(response, HttpStatusCode.NotFound, "El CheckIn solicitado no existe.");

        using var getUser = factory.CreateClient();
        var getResponse = await SendAsync(getUser, HttpMethod.Get, $"/check-ins/{ownedId}", userToken);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WithoutOrWithGarbageToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);
        var payload = new { revision = 1 };

        using var anonymousClient = factory.CreateClient();
        var anonymousResponse = await SendAsync(anonymousClient, HttpMethod.Delete,
            $"/check-ins/{checkInId}", null, payload);
        using var garbageClient = factory.CreateClient();
        var garbageResponse = await SendAsync(garbageClient, HttpMethod.Delete,
            $"/check-ins/{checkInId}", "garbage-token", payload);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbageResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WithRevokedToken_ShouldReturn401_Regression()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        await RevokeAccessTokenAsync(client, accessToken);

        using var client2 = factory.CreateClient();
        var response = await SendAsync(client2, HttpMethod.Delete, $"/check-ins/{checkInId}", accessToken,
            new { revision = 1 });
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized,
            "no es válido, ha expirado o ha sido revocado");
    }

    [Fact]
    public async Task Delete_WithoutOrWithNonPositiveRevision_ShouldReturn400()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId);

        using var emptyBodyClient = factory.CreateClient();
        var missingRevision = await SendAsync(emptyBodyClient, HttpMethod.Delete,
            $"/check-ins/{checkInId}", accessToken, new { });
        using var zeroRevisionClient = factory.CreateClient();
        var zeroRevision = await SendAsync(zeroRevisionClient, HttpMethod.Delete,
            $"/check-ins/{checkInId}", accessToken, new { revision = 0 });

        await AssertProblemAsync(missingRevision, HttpStatusCode.BadRequest, "obligatoria");
        await AssertProblemAsync(zeroRevision, HttpStatusCode.BadRequest, "obligatoria");

        // El recurso permanece intacto tras las validaciones rechazadas
        using var after = await GetRepresentationAsync(factory, checkInId, accessToken);
        Assert.Equal(1, after.RootElement.GetProperty("revision").GetInt32());
    }

    // ------------------------------------------------------------------------------------------
    // GET /check-ins (F2c / OQ-PROD-019): keyset, cursor Base64URL versionado, límites
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task List_WithoutCheckIns_ShouldReturn200WithEmptyItemsAndNullCursor()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);

        using var listClient = factory.CreateClient();
        var response = await SendAsync(listClient, HttpMethod.Get, "/check-ins", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (items, nextCursor) = await ReadPageAsync(response);
        Assert.Empty(items);
        Assert.Null(nextCursor);
    }

    [Fact]
    public async Task List_ShouldReturnItemsWithFieldsIdenticalToGetById()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var checkInId = await CreateCheckInAsync(factory, accessToken, dimensionId,
            value: 4, note: "Detalle F2c");

        using var representation = await GetRepresentationAsync(factory, checkInId, accessToken);
        using var listClient = factory.CreateClient();
        var response = await SendAsync(listClient, HttpMethod.Get, "/check-ins?limit=1", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (items, nextCursor) = await ReadPageAsync(response);
        var item = Assert.Single(items);
        Assert.Null(nextCursor);
        Assert.Equal(representation.RootElement.GetRawText(), item.GetRawText());
    }

    [Fact]
    public async Task List_WithTiedRecordedAt_ShouldWalkEveryPageExactlyOnce()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-4);
        var expected = new HashSet<Guid>
        {
            await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime),
            await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(10)),
            await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(10)),
            await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(20)),
            await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(30))
        };

        var visited = new List<JsonElement>();
        var page = 0;
        string? cursor = null;
        do
        {
            var url = $"/check-ins?limit=2{(cursor is null ? string.Empty : $"&cursor={cursor}")}";
            using var listClient = factory.CreateClient();
            var response = await SendAsync(listClient, HttpMethod.Get, url, accessToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var (items, nextCursor) = await ReadPageAsync(response);
            visited.AddRange(items);
            page++;
            cursor = nextCursor;
        } while (cursor is not null && page < 10);

        Assert.Null(cursor);
        Assert.Equal(3, page);
        Assert.Equal(5, visited.Count);
        Assert.True(expected.SetEquals(
            visited.Select(item => item.GetProperty("checkInId").GetGuid())));
        for (var index = 1; index < visited.Count; index++)
        {
            Assert.True(
                visited[index - 1].GetProperty("recordedAt").GetDateTimeOffset() >=
                visited[index].GetProperty("recordedAt").GetDateTimeOffset());
        }
    }

    [Fact]
    public async Task List_WithoutLimit_ShouldApplyDefaultLimitOfTwenty()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        for (var index = 0; index < 21; index++)
        {
            await CreateCheckInAsync(factory, accessToken, dimensionId);
        }

        using var listClient = factory.CreateClient();
        var response = await SendAsync(listClient, HttpMethod.Get, "/check-ins", accessToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (items, nextCursor) = await ReadPageAsync(response);
        Assert.Equal(20, items.Count);
        Assert.False(string.IsNullOrEmpty(nextCursor));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task List_WithOutOfRangeLimit_ShouldReturn400(int limit)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);

        using var listClient = factory.CreateClient();
        var response = await SendAsync(listClient, HttpMethod.Get, $"/check-ins?limit={limit}", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "limit");
    }

    [Fact]
    public async Task List_WithNonIntegerLimit_ShouldReturn400Problem()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);

        using var listClient = factory.CreateClient();
        var response = await SendAsync(listClient, HttpMethod.Get, "/check-ins?limit=abc", accessToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("no-es-base64!")]
    [InlineData("YWJjZA==")]
    public async Task List_WithMalformedCursor_ShouldReturn400Uniform(string cursor)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);

        using var listClient = factory.CreateClient();
        var response = await SendAsync(
            listClient, HttpMethod.Get, $"/check-ins?cursor={cursor}", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest,
            "El cursor proporcionado no es válido.");
    }

    [Theory]
    [InlineData("2|2026-10-09T15:04:05.1234567+00:00|550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("1|2026-10-09T15:04:05.1234567+00:00")]
    [InlineData("1|not-a-timestamp|550e8400-e29b-41d4-a716-446655440000")]
    public async Task List_WithWellFormedButInvalidCursorPayload_ShouldReturn400Uniform(string plaintext)
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var cursor = ToBase64Url(plaintext);

        using var listClient = factory.CreateClient();
        var response = await SendAsync(
            listClient, HttpMethod.Get, $"/check-ins?cursor={cursor}", accessToken);

        await AssertProblemAsync(response, HttpStatusCode.BadRequest,
            "El cursor proporcionado no es válido.");
    }

    [Fact]
    public async Task List_WithoutOrWithGarbageToken_ShouldReturn401()
    {
        using var factory = CreateFactory();
        using var anonymousClient = factory.CreateClient();
        using var garbageClient = factory.CreateClient();

        var anonymousResponse = await SendAsync(anonymousClient, HttpMethod.Get, "/check-ins", null);
        var garbageResponse = await SendAsync(
            garbageClient, HttpMethod.Get, "/check-ins", "garbage-token");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, garbageResponse.StatusCode);
    }

    [Fact]
    public async Task List_WithRevokedToken_ShouldReturn401_Regression()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        await CreateCheckInAsync(factory, accessToken, dimensionId);

        await RevokeAccessTokenAsync(client, accessToken);

        using var client2 = factory.CreateClient();
        var response = await SendAsync(client2, HttpMethod.Get, "/check-ins", accessToken);
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized,
            "no es válido, ha expirado o ha sido revocado");
    }

    [Fact]
    public async Task List_WithAdministratorToken_ShouldReturnOnlyOwnCheckIns_RN026()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (adminUserId, adminToken) = await CreateSessionAsync(client);
        var (_, userToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var adminCheckInId = await CreateCheckInAsync(factory, adminToken, dimensionId);
        var foreignCheckInId = await CreateCheckInAsync(factory, userToken, dimensionId);
        await EnableAdministratorAsync(adminUserId);

        using var listClient = factory.CreateClient();
        var response = await SendAsync(listClient, HttpMethod.Get, "/check-ins", adminToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var (items, _) = await ReadPageAsync(response);
        var returned = items.Select(item => item.GetProperty("checkInId").GetGuid()).ToArray();
        Assert.Contains(adminCheckInId, returned);
        Assert.DoesNotContain(foreignCheckInId, returned);
    }

    [Fact]
    public async Task List_ShouldNeverReturnForeignCheckInsAcrossPages()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, ownerToken) = await CreateSessionAsync(client);
        var (_, foreignToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-4);
        var expected = new HashSet<Guid>();
        for (var index = 0; index < 3; index++)
        {
            expected.Add(await CreateCheckInAsync(factory, ownerToken, dimensionId,
                recordedAt: baseTime.AddMinutes(index * 30)));
            await CreateCheckInAsync(factory, foreignToken, dimensionId,
                recordedAt: baseTime.AddMinutes(index * 30 + 10));
        }

        var visited = new List<Guid>();
        string? cursor = null;
        var page = 0;
        do
        {
            var url = $"/check-ins?limit=1{(cursor is null ? string.Empty : $"&cursor={cursor}")}";
            using var listClient = factory.CreateClient();
            var response = await SendAsync(listClient, HttpMethod.Get, url, ownerToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var (items, nextCursor) = await ReadPageAsync(response);
            visited.AddRange(items.Select(item => item.GetProperty("checkInId").GetGuid()));
            cursor = nextCursor;
            page++;
        } while (cursor is not null && page < 10);

        Assert.True(expected.SetEquals(visited));
    }

    [Fact]
    public async Task List_AfterConcurrentInsertion_ShouldNotShiftSubsequentPages()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-3);
        var oldest = await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime);
        var middle = await CreateCheckInAsync(
            factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(30));
        var newest = await CreateCheckInAsync(
            factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(60));

        using var firstClient = factory.CreateClient();
        var first = await SendAsync(firstClient, HttpMethod.Get, "/check-ins?limit=1", accessToken);
        var (firstItems, firstCursor) = await ReadPageAsync(first);
        Assert.Equal(newest, Assert.Single(firstItems).GetProperty("checkInId").GetGuid());
        Assert.False(string.IsNullOrEmpty(firstCursor));

        var inserted = await CreateCheckInAsync(factory, accessToken, dimensionId);

        using var secondClient = factory.CreateClient();
        var second = await SendAsync(
            secondClient, HttpMethod.Get, $"/check-ins?limit=1&cursor={firstCursor}", accessToken);
        var (secondItems, secondCursor) = await ReadPageAsync(second);
        Assert.Equal(middle, Assert.Single(secondItems).GetProperty("checkInId").GetGuid());

        using var thirdClient = factory.CreateClient();
        var third = await SendAsync(
            thirdClient, HttpMethod.Get, $"/check-ins?limit=1&cursor={secondCursor}", accessToken);
        var (thirdItems, thirdCursor) = await ReadPageAsync(third);
        Assert.Equal(oldest, Assert.Single(thirdItems).GetProperty("checkInId").GetGuid());
        Assert.Null(thirdCursor);

        var visited = new List<Guid>
        {
            firstItems[0].GetProperty("checkInId").GetGuid(),
            secondItems[0].GetProperty("checkInId").GetGuid(),
            thirdItems[0].GetProperty("checkInId").GetGuid()
        };
        Assert.DoesNotContain(inserted, visited);
    }

    [Fact]
    public async Task List_AfterDeleteAcrossCursor_ShouldSkipDeletedRowWithoutError()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-3);
        var oldest = await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime);
        var middle = await CreateCheckInAsync(
            factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(30));
        var newest = await CreateCheckInAsync(
            factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(60));

        using var firstClient = factory.CreateClient();
        var first = await SendAsync(firstClient, HttpMethod.Get, "/check-ins?limit=1", accessToken);
        var (firstItems, firstCursor) = await ReadPageAsync(first);
        Assert.Equal(newest, Assert.Single(firstItems).GetProperty("checkInId").GetGuid());

        using var deleteClient = factory.CreateClient();
        var deleted = await SendAsync(deleteClient, HttpMethod.Delete,
            $"/check-ins/{newest}", accessToken, new { revision = 1 });
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        using var secondClient = factory.CreateClient();
        var second = await SendAsync(
            secondClient, HttpMethod.Get, $"/check-ins?limit=1&cursor={firstCursor}", accessToken);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var (secondItems, secondCursor) = await ReadPageAsync(second);
        Assert.Equal(middle, Assert.Single(secondItems).GetProperty("checkInId").GetGuid());

        using var thirdClient = factory.CreateClient();
        var third = await SendAsync(
            thirdClient, HttpMethod.Get, $"/check-ins?limit=1&cursor={secondCursor}", accessToken);
        var (thirdItems, thirdCursor) = await ReadPageAsync(third);
        Assert.Equal(oldest, Assert.Single(thirdItems).GetProperty("checkInId").GetGuid());
        Assert.Null(thirdCursor);
    }

    [Fact]
    public async Task List_AfterPutMovingRecordedAtAcrossCursor_ShouldOmitUnvisitedRow_DocumentedKeysetLimitation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var (_, accessToken) = await CreateSessionAsync(client);
        var (dimensionId, _) = await SeedDimensionAsync();
        var baseTime = DateTimeOffset.UtcNow.AddHours(-5);
        var oldest = await CreateCheckInAsync(factory, accessToken, dimensionId, recordedAt: baseTime);
        var moved = await CreateCheckInAsync(
            factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(30));
        var anchor = await CreateCheckInAsync(
            factory, accessToken, dimensionId, recordedAt: baseTime.AddMinutes(60));

        using var firstClient = factory.CreateClient();
        var first = await SendAsync(firstClient, HttpMethod.Get, "/check-ins?limit=1", accessToken);
        var (firstItems, firstCursor) = await ReadPageAsync(first);
        Assert.Equal(anchor, Assert.Single(firstItems).GetProperty("checkInId").GetGuid());

        using var movedRepresentation = await GetRepresentationAsync(factory, moved, accessToken);
        var movedCreatedAt = movedRepresentation.RootElement.GetProperty("createdAt").GetDateTimeOffset();
        using var putClient = factory.CreateClient();
        var put = await SendAsync(putClient, HttpMethod.Put, $"/check-ins/{moved}", accessToken, new
        {
            revision = 1,
            recordedAt = movedCreatedAt,
            note = "Reubicado",
            measurements = new[] { new { dimensionId, value = 4 } }
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        using var secondClient = factory.CreateClient();
        var second = await SendAsync(
            secondClient, HttpMethod.Get, $"/check-ins?limit=10&cursor={firstCursor}", accessToken);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var (secondItems, secondCursor) = await ReadPageAsync(second);
        Assert.Null(secondCursor);
        Assert.Equal(new[] { oldest },
            secondItems.Select(item => item.GetProperty("checkInId").GetGuid()).ToArray());
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

    private async Task<Guid> CreateCheckInAsync(
        WebApplicationFactory<Program> factory,
        string accessToken,
        Guid dimensionId,
        int value = 3,
        string? note = "Original",
        DateTimeOffset? recordedAt = null)
    {
        using var client = factory.CreateClient();
        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
            recordedAt,
            note,
            measurements = new[] { new { dimensionId, value } }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadGuidAsync(response, "checkInId");
    }

    private async Task<JsonDocument> GetRepresentationAsync(
        WebApplicationFactory<Program> factory,
        Guid checkInId,
        string accessToken)
    {
        using var client = factory.CreateClient();
        var response = await SendAsync(client, HttpMethod.Get, $"/check-ins/{checkInId}", accessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    /// <summary>Desplaza created_at y recorded_at hacia el pasado conservando la ventana física.</summary>
    private async Task ShiftEditWindowAsync(Guid checkInId, int hours)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE yusay.check_in
            SET created_at = created_at - (@Hours::double precision * interval '1 hour'),
                recorded_at = recorded_at - (@Hours::double precision * interval '1 hour')
            WHERE check_in_id = @CheckInId;
            """, new { CheckInId = checkInId, Hours = hours }));
    }

    private async Task RetireDimensionVersionAsync(Guid versionId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE yusay.dimension_version SET status = 'RETIRED' WHERE dimension_version_id = @VersionId;",
            new { VersionId = versionId }));
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

    private static async Task<(IReadOnlyList<JsonElement> Items, string? NextCursor)> ReadPageAsync(
        HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = body.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(item => item.Clone())
            .ToArray();
        var nextCursorElement = body.RootElement.GetProperty("nextCursor");
        var nextCursor = nextCursorElement.ValueKind == JsonValueKind.Null
            ? null
            : nextCursorElement.GetString();
        return (items, nextCursor);
    }

    private static string ToBase64Url(string plaintext) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

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
