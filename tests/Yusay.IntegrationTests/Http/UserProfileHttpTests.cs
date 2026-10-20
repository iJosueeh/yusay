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
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;
using Yusay.Application.Common.Interfaces;

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

    private static Task<HttpResponseMessage> SendDeleteAsync(HttpClient client, string path, object? body = null, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, path);
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

    // ==========================================================================================
    // Pruebas de integración HTTP de RF-004b (DELETE /users/me)
    // ==========================================================================================

    [Fact]
    public async Task DeleteAccount_WithoutToken_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await SendDeleteAsync(client, "/users/me", new { password = ValidPassword });

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DeleteAccount_WithEmptyPassword_ShouldReturn400BadRequest()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"del_empty_pass_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendDeleteAsync(client, "/users/me", new { password = "" }, accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task DeleteAccount_WithWrongPassword_ShouldReturn401Unauthorized()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"del_wrong_pass_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendDeleteAsync(client, "/users/me", new { password = "WrongPassword#999" }, accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // La cuenta debe seguir intacta en BD
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.user_account WHERE user_id = @UserId",
            new { UserId = userId });
        Assert.Equal(1, exists);
    }

    [Fact]
    public async Task DeleteAccount_WithValidPassword_ShouldReturn204AndPurgePersonalData()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"del_success_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendDeleteAsync(client, "/users/me", new { password = ValidPassword }, accessToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Comprobar purga física en PostgreSQL mediante cascadas
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        var userCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.user_account WHERE user_id = @UserId", new { UserId = userId });
        Assert.Equal(0, userCount);

        var credCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.user_credential WHERE user_id = @UserId", new { UserId = userId });
        Assert.Equal(0, credCount);

        var tokenCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.email_verification_token WHERE user_id = @UserId", new { UserId = userId });
        Assert.Equal(0, tokenCount);

        // Peticiones subsecuentes con el mismo token deben ser rechazadas (401)
        var nextGet = await SendGetAsync(client, "/users/me", accessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, nextGet.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ShouldDisassociateAuditEvents()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"del_disassociate_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Verificar existencia previa de eventos de auditoría vinculados al usuario
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            var priorTargetCount = await conn.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM yusay.audit_event WHERE target_identifier = @TargetId",
                new { TargetId = userId.ToString() });
            Assert.True(priorTargetCount > 0, "Deben existir eventos previos con target_identifier del usuario.");
        }

        // Act: suprimir la cuenta
        var response = await SendDeleteAsync(client, "/users/me", new { password = ValidPassword }, accessToken);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Assert: comprobar desvinculación tras supresión
        await using (var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            // Trigger V021 desvinculó target_identifier
            var targetCount = await conn.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM yusay.audit_event WHERE target_identifier = @TargetId",
                new { TargetId = userId.ToString() });
            Assert.Equal(0, targetCount);

            // FK ON DELETE SET NULL desvinculó actor_user_id
            var actorCount = await conn.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM yusay.audit_event WHERE actor_user_id = @UserId",
                new { UserId = userId });
            Assert.Equal(0, actorCount);

            // Evento USER_DELETED emitido conforme a norma
            var userDeletedRow = await conn.QuerySingleOrDefaultAsync<dynamic>(
                "SELECT * FROM yusay.audit_event WHERE action = 'USER_DELETED' ORDER BY occurred_at DESC LIMIT 1");
            Assert.NotNull(userDeletedRow);
            var rowDict = (IDictionary<string, object?>)userDeletedRow!;
            Assert.Equal("USER", rowDict["actor_kind"]?.ToString());
            Assert.Null(rowDict["actor_user_id"]);
            Assert.Equal("USER", rowDict["target_type"]?.ToString());
            Assert.Null(rowDict["target_identifier"]);
            Assert.Null(rowDict["metadata"]);
        }
    }

    [Fact]
    public async Task DeleteAccount_ShouldPreserveLeastPrivilegesOnAuditEvent()
    {
        // Arrange: conectarse como yusay_app
        var appConnStr = _fixture.BuildRoleConnectionString("yusay_app");
        await using var conn = new NpgsqlConnection(appConnStr);
        await conn.OpenAsync();

        // 1. yusay_app no puede ejecutar UPDATE manual sobre audit_event (V015 blindado)
        var updateEx = await Assert.ThrowsAsync<PostgresException>(() =>
            conn.ExecuteAsync("UPDATE yusay.audit_event SET target_identifier = 'illegal' WHERE true;"));
        Assert.Equal("42501", updateEx.SqlState);

        // 2. yusay_app no puede ejecutar DELETE manual sobre audit_event (V015 blindado)
        var deleteEx = await Assert.ThrowsAsync<PostgresException>(() =>
            conn.ExecuteAsync("DELETE FROM yusay.audit_event;"));
        Assert.Equal("42501", deleteEx.SqlState);

        // 3. yusay_app no puede invocar directamente la función trigger (REVOKE ALL FROM PUBLIC, sin GRANT a yusay_app)
        var executeEx = await Assert.ThrowsAsync<PostgresException>(() =>
            conn.ExecuteAsync("SELECT yusay.fn_disassociate_audit_events_on_user_account_deletion();"));
        Assert.Equal("42501", executeEx.SqlState);

        // 4. Verificar configuración de seguridad en pg_proc (SECURITY DEFINER y search_path seguro)
        await using var verifyConn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var (isSecDef, proConfig, hasExecutePrivilege) = await verifyConn.QuerySingleAsync<(bool, string[], bool)>(
            @"SELECT p.prosecdef, p.proconfig,
                     has_function_privilege('yusay_app', 'yusay.fn_disassociate_audit_events_on_user_account_deletion()', 'EXECUTE')
              FROM pg_proc p
              JOIN pg_namespace n ON n.oid = p.pronamespace
              WHERE n.nspname = 'yusay' AND p.proname = 'fn_disassociate_audit_events_on_user_account_deletion';");

        Assert.True(isSecDef, "La función debe ser SECURITY DEFINER.");
        Assert.Contains("search_path=pg_catalog, pg_temp", proConfig);
        Assert.False(hasExecutePrivilege, "yusay_app NO debe tener privilegios EXECUTE directos sobre la función.");

        // 5. El trigger se dispara exitosamente cuando yusay_app ejecuta DELETE en user_account
        var testUserId = Guid.NewGuid();
        await verifyConn.ExecuteAsync(
            @"INSERT INTO yusay.user_account (user_id, email, created_at, status, adult_confirmed_at)
              VALUES (@UserId, @Email, NOW(), 'ACTIVE', NOW());
              INSERT INTO yusay.audit_event (audit_event_id, actor_kind, action, target_type, target_identifier, occurred_at)
              VALUES (gen_random_uuid(), 'USER', 'USER_REGISTERED', 'USER', @TargetId, NOW());",
            new { UserId = testUserId, Email = $"priv_{testUserId:N}@yusay.local", TargetId = testUserId.ToString() });

        var rowsDeleted = await conn.ExecuteAsync(
            "DELETE FROM yusay.user_account WHERE user_id = @UserId",
            new { UserId = testUserId });
        Assert.Equal(1, rowsDeleted);

        var remainingCount = await verifyConn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.audit_event WHERE target_identifier = @TargetId",
            new { TargetId = testUserId.ToString() });
        Assert.Equal(0, remainingCount);
    }

    [Fact]
    public async Task DeleteAccount_WhenAuditInsertFails_ShouldStillDeleteAccountUnderDpTrans001()
    {
        // Arrange: factory con repositorio de auditoría que falla en USER_DELETED
        using var factory = CreateFactoryWithFailingAudit();
        using var client = factory.CreateClient();

        string email = $"del_dp_trans_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendDeleteAsync(client, "/users/me", new { password = ValidPassword }, accessToken);

        // Assert: 204 exitoso a pesar del fallo en auditoría (DP-TRANS-001)
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // La cuenta fue eliminada físicamente de PostgreSQL
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.user_account WHERE user_id = @UserId",
            new { UserId = userId });
        Assert.Equal(0, exists);
    }

    [Fact]
    public async Task DeleteAccount_WhenRedisFails_ShouldStillDeleteAccountAndBlockSubsequentRequests()
    {
        // Arrange: WebApplicationFactory donde RevokeAsync en Redis falla post-commit
        using var factory = CreateFactoryWithFailingRedisRevocation();
        using var client = factory.CreateClient();

        string email = $"del_redis_fail_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendDeleteAsync(client, "/users/me", new { password = ValidPassword }, accessToken);

        // Assert: 204 exitoso
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // La cuenta fue eliminada en PostgreSQL
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.user_account WHERE user_id = @UserId",
            new { UserId = userId });
        Assert.Equal(0, exists);

        // Siguiente petición es rechazada con 401 porque el usuario ya no existe en BD
        var nextGet = await SendGetAsync(client, "/users/me", accessToken);
        Assert.Equal(HttpStatusCode.Unauthorized, nextGet.StatusCode);
    }

    [Fact]
    public async Task DeleteAccount_ConcurrentDeletions_ShouldHandleGracefully()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        string email = $"del_concurrent_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act: dos peticiones de eliminación concurrentes con el mismo token
        var task1 = SendDeleteAsync(client, "/users/me", new { password = ValidPassword }, accessToken);
        var task2 = SendDeleteAsync(client, "/users/me", new { password = ValidPassword }, accessToken);

        var responses = await Task.WhenAll(task1, task2);

        // Assert: al menos una debe responder 204 No Content
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.NoContent);

        // Ninguna debe responder 500
        Assert.DoesNotContain(responses, r => r.StatusCode == HttpStatusCode.InternalServerError);

        // La cuenta está eliminada
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.user_account WHERE user_id = @UserId",
            new { UserId = userId });
        Assert.Equal(0, exists);
    }

    private WebApplicationFactory<Program> CreateFactoryWithFailingAudit() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IAuditEventRepository));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddScoped<IAuditEventRepository>(sp =>
                    new FailingUserDeletedAuditRepository(
                        new AuditEventRepository(sp.GetRequiredService<IDbConnectionFactory>())));
            });
        });

    private WebApplicationFactory<Program> CreateFactoryWithFailingRedisRevocation() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
            builder.ConfigureServices(services =>
            {
                var descriptor = services.FirstOrDefault(s => s.ServiceType == typeof(IAccessTokenDenylist));
                if (descriptor != null)
                {
                    services.Remove(descriptor);
                }
                services.AddSingleton<IAccessTokenDenylist, FailingOnRevokeAccessTokenDenylist>();
            });
        });

    private sealed class FailingUserDeletedAuditRepository(AuditEventRepository inner) : IAuditEventRepository
    {
        private readonly AuditEventRepository _inner = inner;

        public Task AddAsync(
            Yusay.Domain.Audit.Entities.AuditEvent auditEvent,
            System.Data.Common.DbTransaction? transaction = null,
            CancellationToken cancellationToken = default)
        {
            if (auditEvent.Action == "USER_DELETED")
            {
                throw new NpgsqlException("Simulación de fallo de cuota/almacenamiento en audit_event.");
            }

            return _inner.AddAsync(auditEvent, transaction, cancellationToken);
        }
    }

    private sealed class FailingOnRevokeAccessTokenDenylist : IAccessTokenDenylist
    {
        public Task<bool> IsRevokedAsync(string tokenId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(false);
        }

        public Task<bool> RevokeAsync(string tokenId, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulación de fallo de conexión o tiempo de espera en Redis.");
        }
    }
}
