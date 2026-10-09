using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yusay.Application.Common.Interfaces;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Pruebas de integración HTTP de la exposición inicial del módulo de identidad
/// (<c>POST /auth/register</c> y <c>POST /auth/verify-email</c>) contra la API real
/// (WebApplicationFactory) con PostgreSQL de Testcontainers.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class IdentityEndpointsHttpTests : IDisposable
{
    private const string TestJwtSecret =
        "yusay-http-integration-test-secret-0123456789abcdef-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

    private readonly PostgreSqlFixture _fixture;
    private readonly CapturingEmailVerificationSender _emailSender = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public IdentityEndpointsHttpTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _factory = CreateFactory(_emailSender);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Register_WithValidPayload_ShouldReturn201WithoutExposingTheVerificationToken()
    {
        // Act
        var email = $"http_reg_{Guid.NewGuid():N}@yusay.local";
        var response = await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = ValidPassword,
            adultConfirmed = true
        });

        // Assert: 201 con Location y cuerpo público sin token ni contraseña
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.StartsWith("/auth/users/", response.Headers.Location!.ToString());

        var body = await response.Content.ReadAsStringAsync();
        var payload = await response.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload.Id);
        Assert.Equal(email.ToLowerInvariant(), payload.Email);
        Assert.DoesNotContain("verificationToken", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);

        // Assert: la entrega del token se realizó por la abstracción, con el token en claro
        var delivery = Assert.Single(_emailSender.Deliveries);
        Assert.Equal(email.ToLowerInvariant(), delivery.Email);
        Assert.False(string.IsNullOrWhiteSpace(delivery.Token));
        Assert.DoesNotContain(delivery.Token, body);

        // Assert end-to-end en PostgreSQL real: cuenta creada y aún sin verificar
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var userRow = await connection.QuerySingleAsync(
            """
            SELECT user_id, (email_verified_at IS NOT NULL) AS verified
            FROM yusay.user_account
            WHERE lower(email) = lower(@Email)
            """,
            new { Email = email });
        Assert.Equal(payload.Id, (Guid)userRow.user_id);
        Assert.False((bool)userRow.verified);
    }

    [Fact]
    public async Task Register_WithInvalidPayloads_ShouldReturn400ProblemDetails()
    {
        var cases = new (object Payload, string ExpectedDetail)[]
        {
            (new
            {
                email = $"http_val_{Guid.NewGuid():N}@yusay.local",
                password = ValidPassword,
                adultConfirmed = false
            }, "mayoría de edad"),
            (new
            {
                email = $"http_val_{Guid.NewGuid():N}@yusay.local",
                password = "short",
                adultConfirmed = true
            }, "8 caracteres"),
            (new
            {
                email = "not-an-email",
                password = ValidPassword,
                adultConfirmed = true
            }, "correo electrónico")
        };

        foreach (var (payload, expectedDetail) in cases)
        {
            var response = await _client.PostAsJsonAsync("/auth/register", payload);
            await AssertProblemAsync(response, HttpStatusCode.BadRequest, expectedDetail);
        }

        Assert.Empty(_emailSender.Deliveries);
    }

    [Fact]
    public async Task Register_WithDuplicatedEmail_ShouldReturn409ProblemDetailsAndDeliverOnlyOnce()
    {
        // Arrange: primera inscripción correcta
        var email = $"http_dup_{Guid.NewGuid():N}@yusay.local";
        var first = await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = ValidPassword,
            adultConfirmed = true
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Act: mismo correo otra vez
        var second = await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = ValidPassword,
            adultConfirmed = true
        });

        // Assert: 409 con ProblemDetails y sin una segunda entrega de token
        await AssertProblemAsync(second, HttpStatusCode.Conflict, "Ya existe una cuenta");
        Assert.Single(_emailSender.Deliveries);
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ShouldReturn400ProblemDetails()
    {
        // Token desconocido
        var unknown = await _client.PostAsJsonAsync("/auth/verify-email", new
        {
            token = $"invalid_{Guid.NewGuid():N}"
        });
        await AssertProblemAsync(unknown, HttpStatusCode.BadRequest, "no existe o ya ha sido consumido");

        // Token vacío
        var empty = await _client.PostAsJsonAsync("/auth/verify-email", new { token = "" });
        await AssertProblemAsync(empty, HttpStatusCode.BadRequest, "obligatorio");
    }

    [Fact]
    public async Task VerifyEmail_WithValidToken_ShouldVerifyTheAccountAndConsumeTheToken()
    {
        // Arrange: registro que entrega el token por la abstracción de correo
        var email = $"http_ver_{Guid.NewGuid():N}@yusay.local";
        var register = await _client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = ValidPassword,
            adultConfirmed = true
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        var delivery = Assert.Single(_emailSender.Deliveries);

        // Act: verificación con el token entregado
        var verify = await _client.PostAsJsonAsync("/auth/verify-email", new { token = delivery.Token });

        // Assert: 200 con datos públicos y sin secretos
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var body = await verify.Content.ReadAsStringAsync();
        var payload = await verify.Content.ReadFromJsonAsync<VerifyResponse>();
        Assert.NotNull(payload);
        Assert.NotEqual(Guid.Empty, payload.UserId);
        Assert.Equal(email.ToLowerInvariant(), payload.Email);
        Assert.NotEqual(default, payload.VerifiedAt);
        Assert.DoesNotContain(delivery.Token, body);

        // Assert end-to-end: cuenta verificada en PostgreSQL real
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var verified = await connection.QuerySingleAsync<bool>(
            "SELECT email_verified_at IS NOT NULL FROM yusay.user_account WHERE user_id = @Id",
            new { Id = payload.UserId });
        Assert.True(verified);

        // Assert: token de un solo uso — reutilizarlo vuelve a fallar sin nueva verificación
        var replay = await _client.PostAsJsonAsync("/auth/verify-email", new { token = delivery.Token });
        await AssertProblemAsync(replay, HttpStatusCode.BadRequest, "no existe o ya ha sido consumido");
    }

    [Fact]
    public async Task SignOut_WithoutAuthorizationHeader_ShouldReturn401ProblemDetails()
    {
        // Act: sin cabecera Authorization
        var response = await _client.PostAsync("/auth/sign-out", null);

        // Assert: la guardia de cabecera sigue respondiendo 401 (mismo código que antes del
        // traslado), ahora con el ProblemDetails centralizado en lugar del 401 sin cuerpo
        // que MVC no permite reproducir (ClientErrorResultFilter lo convertiría en otro
        // formato igualmente). No se invoca el caso de uso.
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "token de acceso Bearer");
    }

    [Fact]
    public async Task SignOut_WithMalformedBearerToken_ShouldReturn401ProblemDetails()
    {
        // Act: token que no valida como JWT (el fallo lo emite el caso de uso)
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/sign-out");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer",
            $"invalid_{Guid.NewGuid():N}");
        var response = await _client.SendAsync(request);

        // Assert: 401 con ProblemDetails centralizado (mismo código que antes del traslado)
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized,
            "no es válido, ha expirado o ha sido revocado");
    }

    [Fact]
    public async Task OpenApi_Document_ShouldPreserveRoutesAndOperationNamesWithoutExposingSecrets()
    {
        // Act: documento OpenAPI publicado en Development
        using var factory = CreateFactory(new CapturingEmailVerificationSender());
        using var client = factory.CreateClient();
        var document = await client.GetStringAsync("/openapi/v1.json");

        using var parsed = JsonDocument.Parse(document);
        var paths = parsed.RootElement.GetProperty("paths");

        // Sin rutas duplicadas: exactamente las rutas acordadas (Identity + F2a CheckIn)
        var routeNames = paths.EnumerateObject()
            .Select(path => path.Name)
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(
            new[]
            {
                "/auth/password-reset/confirm", "/auth/password-reset/request", "/auth/register",
                "/auth/sign-in", "/auth/sign-out", "/auth/verify-email",
                "/check-ins", "/check-ins/{checkInId}", "/health"
            },
            routeNames);

        // Nombres OpenAPI de operación conservados tras la migración a Controllers
        Assert.Equal("RegisterUser", ReadOperationId(paths, "/auth/register", "post"));
        Assert.Equal("RequestPasswordReset", ReadOperationId(paths, "/auth/password-reset/request", "post"));
        Assert.Equal("ResetPassword", ReadOperationId(paths, "/auth/password-reset/confirm", "post"));
        Assert.Equal("SignIn", ReadOperationId(paths, "/auth/sign-in", "post"));
        Assert.Equal("VerifyEmail", ReadOperationId(paths, "/auth/verify-email", "post"));
        Assert.Equal("SignOut", ReadOperationId(paths, "/auth/sign-out", "post"));
        Assert.Equal("CreateCheckIn", ReadOperationId(paths, "/check-ins", "post"));
        Assert.Equal("GetCheckInById", ReadOperationId(paths, "/check-ins/{checkInId}", "get"));
        Assert.Equal("HealthCheck", ReadOperationId(paths, "/health", "get"));

        // sign-out documenta exactamente sus códigos reales: 204 (éxito idempotente) y
        // ProblemDetails 400/401/503 — nunca el 200 fantasma que genera MVC sin metadatos
        var signOutResponses = paths.GetProperty("/auth/sign-out").GetProperty("post")
            .GetProperty("responses").EnumerateObject()
            .Select(response => response.Name)
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(new[] { "204", "400", "401", "503" }, signOutResponses);

        // El contrato de solicitud sigue siendo JSON con el esquema del DTO
        var requestBody = paths.GetProperty("/auth/register").GetProperty("post").GetProperty("requestBody");
        Assert.True(requestBody.GetProperty("content").TryGetProperty("application/json", out var jsonContent));
        Assert.Contains("RegisterUserRequest", jsonContent.GetProperty("schema").GetRawText(), StringComparison.Ordinal);

        // Sin token de verificación en los esquemas ni secretos de configuración
        Assert.DoesNotContain("verificationToken", document, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(TestJwtSecret, document, StringComparison.Ordinal);
    }

    private static string? ReadOperationId(JsonElement paths, string path, string method) =>
        paths.GetProperty(path).GetProperty(method).GetProperty("operationId").GetString();

    /// <summary>
    /// Host de prueba de la API: entorno Development, PostgreSQL del contenedor de la colección,
    /// secreto JWT de prueba y <see cref="IEmailVerificationSender"/> reemplazado por un doble
    /// que captura las entregas (sin proveedor de correo externo).
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory(IEmailVerificationSender emailSender) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailVerificationSender>();
                services.AddSingleton(emailSender);
            });
        });

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedDetailFragment)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.False(string.IsNullOrWhiteSpace(problem.Title));
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.Contains(expectedDetailFragment, problem.Detail!, StringComparison.OrdinalIgnoreCase);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private sealed record RegisterResponse(Guid Id, string Email);

    private sealed record VerifyResponse(Guid UserId, string Email, DateTimeOffset VerifiedAt);

    /// <summary>
    /// Doble que captura las entregas de tokens de verificación en lugar del proveedor de correo.
    /// </summary>
    private sealed class CapturingEmailVerificationSender : IEmailVerificationSender
    {
        private readonly object _sync = new();

        public List<(string Email, string Token)> Deliveries { get; } = new();

        public Task SendVerificationTokenAsync(
            string recipientEmail,
            string verificationToken,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                Deliveries.Add((recipientEmail, verificationToken));
            }

            return Task.CompletedTask;
        }
    }
}
