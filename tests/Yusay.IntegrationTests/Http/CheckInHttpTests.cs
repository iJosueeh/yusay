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
/// Pruebas HTTP de CheckIn (F2a de N1 + F2b-1): creación y lectura propias, edición
/// optimista con ventana absoluta de 168 horas, 404 uniforme para recurso ajeno e
/// inexistente (comparando los campos públicos de ProblemDetails salvo traceId),
/// RN-026 (el administrador no accede a check-ins privados ajenos), validaciones de
/// escala/ventana/mediciones, carrera de dos ediciones con la misma revisión y
/// regresión del gate de autenticación.
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
        string? note = "Original")
    {
        using var client = factory.CreateClient();
        var response = await SendAsync(client, HttpMethod.Post, "/check-ins", accessToken, new
        {
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
