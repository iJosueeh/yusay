using System.Net;
using System.Net.Http.Headers;
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

/// <summary>
/// Pruebas de integración HTTP de RF-004a (<c>GET /users/me</c>) contra la API real
/// (WebApplicationFactory) con PostgreSQL de Testcontainers y Redis de pruebas.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class UserProfileHttpTests : IAsyncLifetime
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

    public UserProfileHttpTests(PostgreSqlFixture fixture)
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
    public async Task GetProfile_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await SendGetAsync(client, "/users/me");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetProfile_WithInvalidToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await SendGetAsync(client, "/users/me", "invalid-token-12345");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task GetProfile_WithValidToken_ShouldReturn200AndAccurateProfile()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"user_me_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendGetAsync(client, "/users/me", accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(userId, root.GetProperty("userId").GetGuid());
        Assert.Equal(email, root.GetProperty("email").GetString());
        Assert.True(root.GetProperty("emailVerified").GetBoolean());
        Assert.True(root.TryGetProperty("createdAt", out var createdAtProp) && createdAtProp.GetString() != null);
        Assert.True(root.TryGetProperty("adultConfirmedAt", out var adultProp) && adultProp.GetString() != null);
        Assert.Equal("ACTIVE", root.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetProfile_ShouldNotExposeSensitiveData()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"sensitive_check_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendGetAsync(client, "/users/me", accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();

        // Verificar que no se expongan credenciales, contraseñas, hashes, roles ni tokens en el cuerpo
        var forbiddenTerms = new[]
        {
            "password", "password_hash", "passwordHash", "credential", "hash",
            "salt", "token", "verificationToken", "role", "roles", "administrator"
        };

        using var doc = JsonDocument.Parse(json);
        var propertyNames = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();

        foreach (var prop in propertyNames)
        {
            foreach (var term in forbiddenTerms)
            {
                Assert.False(
                    string.Equals(prop, term, StringComparison.OrdinalIgnoreCase),
                    $"La respuesta 200 de /users/me no debe exponer el campo sensible '{prop}'.");
            }
        }
    }

    [Fact]
    public async Task GetProfile_ShouldIsolateBetweenUsers()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email1 = $"isolation_user1_{Guid.NewGuid():N}@yusay.local";
        string email2 = $"isolation_user2_{Guid.NewGuid():N}@yusay.local";

        var (userId1, token1) = await CreateSessionAsync(email1);
        var (userId2, token2) = await CreateSessionAsync(email2);

        // Act
        var response1 = await SendGetAsync(client, "/users/me", token1);
        var response2 = await SendGetAsync(client, "/users/me", token2);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        using var doc1 = JsonDocument.Parse(await response1.Content.ReadAsStringAsync());
        using var doc2 = JsonDocument.Parse(await response2.Content.ReadAsStringAsync());

        Assert.Equal(userId1, doc1.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(email1, doc1.RootElement.GetProperty("email").GetString());

        Assert.Equal(userId2, doc2.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(email2, doc2.RootElement.GetProperty("email").GetString());

        Assert.NotEqual(userId1, userId2);
    }

    [Fact]
    public async Task GetProfile_ShouldNotGenerateAuditEvents()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"audit_check_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Contar eventos de auditoría previos
        long countBefore = await GetAuditEventCountAsync();

        // Act: consultar perfil
        var response = await SendGetAsync(client, "/users/me", accessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Assert: no se generaron nuevos eventos de auditoría
        long countAfter = await GetAuditEventCountAsync();
        Assert.Equal(countBefore, countAfter);
    }

    private async Task<long> GetAuditEventCountAsync()
    {
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await conn.ExecuteScalarAsync<long>("SELECT count(*) FROM yusay.audit_event;");
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

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync(string email)
    {
        var registration = await new RegisterUserUseCase(
                _unitOfWork,
                _userAccountRepo,
                _userCredentialRepo,
                _verificationTokenRepo,
                _auditEventRepo,
                _passwordHasher,
                new SecureTokenService(),
                new NullEmailVerificationSender())
            .ExecuteAsync(new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));

        await new VerifyEmailUseCase(
                _unitOfWork,
                _userAccountRepo,
                _verificationTokenRepo,
                _auditEventRepo,
                new SecureTokenService())
            .ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));

        var session = await new SignInUseCase(
                _unitOfWork,
                _userAccountRepo,
                _userCredentialRepo,
                _auditEventRepo,
                _passwordHasher,
                _jwtTokenService)
            .ExecuteAsync(new SignInCommand(email, ValidPassword));

        return (registration.UserId, session.AccessToken);
    }

    private static Task<HttpResponseMessage> SendGetAsync(HttpClient client, string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }
}
