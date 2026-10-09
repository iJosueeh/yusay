using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Xunit;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.RequestPasswordReset;
using Yusay.Application.Identity.Commands.ResetPassword;
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
/// Pruebas HTTP del esquema de autenticación Bearer reutilizable: endpoint protegido con
/// <c>[Authorize]</c> (controlador-sonda del ensamblado de pruebas) delegado en
/// <c>IValidateAccessTokenUseCase</c> —sin segunda validación JWT—, distinción de solicitud
/// anónima / token inválido / fallo de infraestructura (401 con detalle propio vs 503
/// fail-closed), conservación de ProblemDetails con <c>traceId</c>, accesibilidad de los
/// endpoints públicos sin token y comportamiento idempotente con auditoría de SignOut.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class BearerAuthenticationHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-http-integration-test-secret-0123456789abcdef-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

    private const string NewPassword = "RecoveredPassword#2026";

    private const string TokenRejectedMessage =
        "El token de acceso no es válido, ha expirado o ha sido revocado.";

    private const string MissingBearerMessage =
        "Se requiere un token de acceso Bearer para acceder a este recurso.";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly PasswordResetTokenRepository _resetTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public BearerAuthenticationHttpTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepo = new UserAccountRepository(fixture.ConnectionFactory);
        _userCredentialRepo = new UserCredentialRepository(fixture.ConnectionFactory);
        _verificationTokenRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
        _resetTokenRepo = new PasswordResetTokenRepository(fixture.ConnectionFactory);
        _auditEventRepo = new AuditEventRepository(fixture.ConnectionFactory);

        // Hasher con opciones ligeras, idéntico al que firma y verifica la contraseña.
        _passwordHasher = new Argon2idPasswordHasher(new Argon2Options
        {
            MemorySize = 1024,
            Iterations = 2,
            DegreeOfParallelism = 1
        });

        // Mismo secreto que recibe la API: los tokens mintiados aquí deben validar allí.
        _jwtTokenService = new JwtTokenService(new JwtOptions
        {
            Secret = TestJwtSecret,
            AccessTokenLifetimeSeconds = 900
        });
    }

    // Redis propio por prueba (patrón de SignOutHttpEndpointsTests): el test fail-closed
    // detiene su contenedor sin afectar al resto.
    public async Task InitializeAsync() => await _redis.InitializeAsync();

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_ShouldReturn401ProblemDetailsWithTraceId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act: solicitud anónima a un recurso protegido
        var response = await SendGetAsync(client, "/test/protected");

        // Assert: distinta de "token inválido" — detalle propio de credencial ausente
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado",
            MissingBearerMessage);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithValidToken_ShouldAuthenticateTheValidatedIdentity()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión real (registro + verificación + signIn)
        string email = $"http_bearer_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendGetAsync(client, "/test/protected", accessToken);

        // Assert: acceso permitido y ClaimsPrincipal construido desde la validación central
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(userId, body.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(email.ToLowerInvariant(), body.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithInvalidAndExpiredTokens_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: token con estructura arbitraria
        var invalid = await SendGetAsync(client, "/test/protected", $"invalid_{Guid.NewGuid():N}");
        await AssertProblemAsync(invalid, HttpStatusCode.Unauthorized, "No autorizado",
            TokenRejectedMessage);

        // Arrange: token firmado con el secreto real, idéntico forma y huella pwd_at, solo exp vencido
        string email = $"http_bearer_exp_{Guid.NewGuid():N}@yusay.local";
        var (userId, _) = await CreateSessionAsync(email);
        var issuedAt = DateTimeOffset.UtcNow;
        var passwordChangedAt = await ReadPasswordChangedAtAsync(userId);
        var expiredToken = CraftToken(
            userId,
            email.ToLowerInvariant(),
            issuedAt.ToUnixTimeSeconds(),
            AccessTokenRevocationPolicy.ToCredentialVersion(passwordChangedAt),
            issuedAt.AddHours(-1).UtcDateTime);

        // Act
        var expired = await SendGetAsync(client, "/test/protected", expiredToken);

        // Assert
        await AssertProblemAsync(expired, HttpStatusCode.Unauthorized, "No autorizado",
            TokenRejectedMessage);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithRevokedTokenAfterSignOut_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión y cierre de sesión (denylist jti en Redis)
        string email = $"http_bearer_revoke_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);
        var signOut = new HttpRequestMessage(HttpMethod.Post, "/auth/sign-out");
        signOut.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var closed = await client.SendAsync(signOut);
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);

        // Act: el mismo token ya no autentica
        var response = await SendGetAsync(client, "/test/protected", accessToken);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado",
            TokenRejectedMessage);
    }

    [Fact]
    public async Task ProtectedEndpoint_AfterPasswordReset_ShouldRejectThePreviousJwt()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente y restablecimiento de contraseña
        string email = $"http_bearer_pwd_{Guid.NewGuid():N}@yusay.local";
        var (userId, previousAccessToken) = await CreateSessionAsync(email);
        var resetRequest = await CreateRequestResetUseCase()
            .ExecuteAsync(new RequestPasswordResetCommand(email));
        Assert.False(string.IsNullOrWhiteSpace(resetRequest.ResetToken));
        await CreateResetPasswordUseCase()
            .ExecuteAsync(new ResetPasswordCommand(resetRequest.ResetToken!, NewPassword));

        // Act: el JWT anterior, invalidado por password_changed_at/pwd_at
        var previous = await SendGetAsync(client, "/test/protected", previousAccessToken);

        // Assert
        await AssertProblemAsync(previous, HttpStatusCode.Unauthorized, "No autorizado",
            TokenRejectedMessage);

        // Control: un token nuevo tras el cambio sigue autenticando (revocación selectiva)
        var session = await CreateSignInUseCase().ExecuteAsync(new SignInCommand(email, NewPassword));
        var fresh = await SendGetAsync(client, "/test/protected", session.AccessToken);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        using var freshBody = JsonDocument.Parse(await fresh.Content.ReadAsStringAsync());
        Assert.Equal(userId, freshBody.RootElement.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task ProtectedEndpoint_WithBlockedAccount_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: token vigente y cuenta bloqueada por decisión administrativa
        string email = $"http_bearer_blocked_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE yusay.user_account SET status = 'BLOCKED' WHERE user_id = @UserId",
                new { UserId = userId });
        }

        // Act
        var response = await SendGetAsync(client, "/test/protected", accessToken);

        // Assert: estado de cuenta verificado por la validación centralizada
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado",
            TokenRejectedMessage);
    }

    [Fact]
    public async Task ProtectedEndpoint_WhenRedisIsUnreachable_ShouldFailClosedWith503ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión y una autenticación exitosa que establish la conexión con Redis
        string email = $"http_bearer_503_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);
        var healthy = await SendGetAsync(client, "/test/protected", accessToken);
        Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);

        // Act: Redis cae y el cliente pierde la conexión establecida
        await _redis.StopAsync();
        var failed = await SendGetAsync(client, "/test/protected", accessToken);

        // Assert: fail-closed — 503 ProblemDetails con traceId, nunca un 200 engañoso
        await AssertProblemAsync(failed, HttpStatusCode.ServiceUnavailable,
            "Servicio no disponible", "no está disponible");
    }

    [Fact]
    public async Task ActionLevelAuthorize_ShouldReturn401WithoutTokenAnd200WithValidJwt()
    {
        // Sonda sin [Authorize] de clase: la protección vive solo en la acción
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente
        string email = $"http_bearer_action_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);

        // Act y Assert: solicitud anónima a la acción protegida
        var anonymous = await SendGetAsync(client, "/test/action/protected");
        await AssertProblemAsync(anonymous, HttpStatusCode.Unauthorized, "No autorizado",
            MissingBearerMessage);

        // Act y Assert: JWT válido autentica la acción
        var authenticated = await SendGetAsync(client, "/test/action/protected", accessToken);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        using var body = JsonDocument.Parse(await authenticated.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task PublicEndpoints_ShouldRemainAccessibleWithoutAuthentication()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Salud sin cabecera de autorización
        var health = await SendGetAsync(client, "/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        // La cabecera Authorization se ignora en endpoints públicos: nunca invalida la petición
        var healthWithGarbageToken = await SendGetAsync(client, "/health", "garbage-token");
        Assert.Equal(HttpStatusCode.OK, healthWithGarbageToken.StatusCode);

        // [AllowAnonymous] dentro de un controlador [Authorize] responde sin token
        var anonymous = await SendGetAsync(client, "/test/protected/anonymous");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        using var body = JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync());
        Assert.False(body.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task SignOut_WithAuthenticationPipeline_ShouldRemainIdempotentAndAuditOnce()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente
        string email = $"http_bearer_so_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act: primer cierre y segundo cierre con el mismo token
        var first = await SendSignOutAsync(client, accessToken);
        var second = await SendSignOutAsync(client, accessToken);

        // Assert: idempotencia 204/204 intacta bajo el pipeline con autenticación registrada
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        // Assert: una única auditoría SIGN_OUT (el segundo SET NX no vuelve a auditar)
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var signOutAudits = await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM yusay.audit_event WHERE action = 'SIGN_OUT' AND actor_user_id = @UserId",
            new { UserId = userId });
        Assert.Equal(1, signOutAudits);
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Host de prueba de la API con el esquema Bearer registrado, PostgreSQL de la colección,
    /// Redis propio, secreto JWT compartido y el controlador-sonda [Authorize] añadido SOLO a
    /// este host (la API real no lo incluye ni su ruta aparece en el documento OpenAPI).
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
            builder.ConfigureServices(services =>
            {
                services.AddControllers()
                    .AddApplicationPart(typeof(ProtectedProbeController).Assembly);
            });
        });

    private RegisterUserUseCase CreateRegisterUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _verificationTokenRepo,
        _auditEventRepo,
        _passwordHasher,
        new SecureTokenService(),
        new NullEmailVerificationSender());

    private VerifyEmailUseCase CreateVerifyEmailUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _verificationTokenRepo,
        _auditEventRepo,
        new SecureTokenService());

    private SignInUseCase CreateSignInUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _auditEventRepo,
        _passwordHasher,
        _jwtTokenService);

    private RequestPasswordResetUseCase CreateRequestResetUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _resetTokenRepo,
        _auditEventRepo,
        new SecureTokenService(),
        new NullPasswordResetEmailSender());

    private ResetPasswordUseCase CreateResetPasswordUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _resetTokenRepo,
        _auditEventRepo,
        _passwordHasher,
        new SecureTokenService());

    private async Task<(Guid UserId, string AccessToken)> CreateSessionAsync(string email)
    {
        var registration = await CreateRegisterUseCase()
            .ExecuteAsync(new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));
        await CreateVerifyEmailUseCase()
            .ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));
        var session = await CreateSignInUseCase()
            .ExecuteAsync(new SignInCommand(email, ValidPassword));
        return (registration.UserId, session.AccessToken);
    }

    private async Task<DateTimeOffset> ReadPasswordChangedAtAsync(Guid userId)
    {
        var credential = await _userCredentialRepo.GetByUserIdAsync(
            userId, transaction: null, CancellationToken.None);
        Assert.NotNull(credential);
        return credential.PasswordChangedAt;
    }

    /// <summary>
    /// Firma un JWT con el secreto de prueba y la misma forma que emite el backend, para variar
    /// una única condición por prueba (aquí, solo <c>exp</c> vencido).
    /// </summary>
    private string CraftToken(
        Guid userId,
        string email,
        long issuedAtSeconds,
        long passwordChangedAtMicroseconds,
        DateTime expiresAt)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(TestJwtSecret)),
            SecurityAlgorithms.HmacSha256);

        var payload = new JwtPayload(
            JwtOptions.DefaultIssuer,
            JwtOptions.DefaultAudience,
            new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString("D")),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D")),
                new Claim(JwtRegisteredClaimNames.Iat, issuedAtSeconds.ToString(), ClaimValueTypes.Integer64),
                new Claim(
                    JwtTokenService.CredentialVersionClaim,
                    passwordChangedAtMicroseconds.ToString(),
                    ClaimValueTypes.Integer64)
            },
            notBefore: null,
            expires: expiresAt);

        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(new JwtHeader(credentials), payload));
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

    private static Task<HttpResponseMessage> SendSignOutAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/sign-out");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedTitle,
        string expectedDetailFragment)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expectedStatus, problem.Status);
        Assert.Equal(expectedTitle, problem.Title);
        Assert.False(string.IsNullOrWhiteSpace(problem.Detail));
        Assert.Contains(expectedDetailFragment, problem.Detail!, StringComparison.Ordinal);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }
}
