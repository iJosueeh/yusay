using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Yusay.Api.Authorization;
using Yusay.Application.Common.Interfaces;
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
/// Pruebas HTTP de la política Administrator (OQ-ARCH-017, bloque B1): 401 sin token o con
/// token inválido, 403 ProblemDetails con traceId para identidad autenticada sin fila en
/// yusay.administrator, autorización con habilitación, 503 fail-closed cuando la comprobación
/// no puede ejecutarse, revocación observable en la siguiente evaluación, precedencia de
/// [AllowAnonymous] y conservación del contrato de los endpoints [Authorize] existentes.
/// Sin auditoría AUTHORIZATION_DENIED en este bloque (decisión pendiente).
/// </summary>
[Collection("DatabaseCollection")]
public sealed class AdminAuthorizationHttpTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-admin-authz-integration-test-secret-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

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
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;

    public AdminAuthorizationHttpTests(PostgreSqlFixture fixture)
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
    public async Task AdminEndpoint_WithoutToken_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendGetAsync(client, "/test/admin/protected");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado",
            MissingBearerMessage);
    }

    [Fact]
    public async Task AdminEndpoint_WithInvalidToken_ShouldReturn401ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await SendGetAsync(client, "/test/admin/protected", $"invalid_{Guid.NewGuid():N}");

        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "No autorizado",
            TokenRejectedMessage);
    }

    [Fact]
    public async Task AdminEndpoint_WithAuthenticatedNonAdministrator_ShouldReturn403ProblemDetailsWithTraceId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente sin fila en yusay.administrator
        string email = $"admin_denied_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendGetAsync(client, "/test/admin/protected", accessToken);

        // Assert: 403 ProblemDetails con traceId — nunca 200, 401 ni 503
        await AssertProblemAsync(response, HttpStatusCode.Forbidden, "Acceso denegado",
            "habilitación administrativa");
    }

    [Fact]
    public async Task AdminEndpoint_WithAdministratorRow_ShouldAuthorize()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente y habilitación administrativa (bootstrap de prueba)
        string email = $"admin_allowed_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);
        await GrantAdministratorAsync(userId);

        // Act
        var response = await SendGetAsync(client, "/test/admin/protected", accessToken);

        // Assert: acceso concedido y identidad del principal conservada
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("isAdministrator").GetBoolean());
        Assert.Equal(userId, body.RootElement.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task AdminEndpoint_WhenAdministratorCheckIsUnavailable_ShouldFailClosedWith503()
    {
        // La comprobación administrativa sustituida por un lector que falla simula la
        // inaccesibilidad de PostgreSQL DURANTE la evaluación (el resto de la petición, en
        // particular la validación del token, sigue usando la base de datos real de la
        // colección, de modo que el 503 proviene exactamente de la comprobación de Autorización.
        using var factory = CreateFactory(services =>
        {
            services.RemoveAll<IAdministratorAuthorizationRepository>();
            services.AddSingleton<IAdministratorAuthorizationRepository, UnreachableAdministratorRepository>();
        });
        using var client = factory.CreateClient();

        // Arrange: sesión vigente
        string email = $"admin_unreachable_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendGetAsync(client, "/test/admin/protected", accessToken);

        // Assert: fail-closed — 503 ProblemDetails con traceId, nunca un 200 ni un 403 engañoso
        await AssertProblemAsync(response, HttpStatusCode.ServiceUnavailable, "Servicio no disponible",
            "no está disponible");
    }

    [Fact]
    public async Task AdminEndpoint_AfterAdministratorRevocation_ShouldDenyTheNextEvaluation()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente habilitada como administradora
        string email = $"admin_revoked_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);
        await GrantAdministratorAsync(userId);

        // Act 1: la habilitación vigente concede acceso
        var authorized = await SendGetAsync(client, "/test/admin/protected", accessToken);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);

        // Act 2: revocación (borrado de la fila) y siguiente evaluación con el MISMO token
        await RevokeAdministratorAsync(userId);
        var denied = await SendGetAsync(client, "/test/admin/protected", accessToken);

        // Assert: la revocación se observa sin depender de la vigencia del token
        await AssertProblemAsync(denied, HttpStatusCode.Forbidden, "Acceso denegado",
            "habilitación administrativa");
    }

    [Fact]
    public async Task AdminEndpoint_AllAnonymousAction_ShouldKeepPrecedenceOverThePolicy()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente SIN habilitación administrativa, para comprobar que
        // AllowAnonymous no exige la política ni siquiera pasando por la sonda administrativa
        string email = $"admin_anon_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);

        // Act y Assert: solicitud anónima a la acción AllowAnonymous de la sonda administrativa
        var anonymous = await SendGetAsync(client, "/test/admin/anonymous");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        using var anonymousBody = JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync());
        Assert.False(anonymousBody.RootElement.GetProperty("isAuthenticated").GetBoolean());

        // Control: con token presente, el contrato existente de BearerAuthenticationHandler
        // sigue sin construir identidad en endpoints AllowAnonymous (NoResult), por lo que
        // la respuesta sigue siendo 200 y sin autenticar — sin regresión por la política nueva
        var authenticated = await SendGetAsync(client, "/test/admin/anonymous", accessToken);
        Assert.Equal(HttpStatusCode.OK, authenticated.StatusCode);
        using var authenticatedBody = JsonDocument.Parse(await authenticated.Content.ReadAsStringAsync());
        Assert.False(authenticatedBody.RootElement.GetProperty("isAuthenticated").GetBoolean());
    }

    [Fact]
    public async Task ExistingAuthorizeEndpoint_ShouldKeepServingAuthenticatedNonAdministrators()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión vigente SIN habilitación administrativa
        string email = $"admin_existing_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act y Assert: el endpoint [Authorize] existente no queda afectado por la política
        // nueva — su comportamiento y el de sus reglas de propiedad siguen intactos
        var response = await SendGetAsync(client, "/test/protected", accessToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(userId, body.RootElement.GetProperty("userId").GetGuid());
        Assert.Equal(email.ToLowerInvariant(), body.RootElement.GetProperty("email").GetString());
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Host de prueba de la API con la política Administrator registrada, PostgreSQL de la
    /// colección, Redis propio y secreto JWT compartido. La sonda administrativa se añade SOLO
    /// a este host desde el ensamblado de pruebas.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory(
        Action<IServiceCollection>? configureServices = null) =>
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
                    .AddApplicationPart(typeof(AdminAuthorizeProbeController).Assembly);
                configureServices?.Invoke(services);
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

    private async Task GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
    }

    private async Task RevokeAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = userId });
    }

    private static Task<HttpResponseMessage> SendGetAsync(
        HttpClient client, string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

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

    /// <summary>
    /// Sustituto que simula la inaccesibilidad de PostgreSQL durante la comprobación
    /// administrativa: el handler debe traducir el fallo en 503 fail-closed sin conceder acceso.
    /// </summary>
    private sealed class UnreachableAdministratorRepository : IAdministratorAuthorizationRepository
    {
        public Task<bool> IsAdministratorAsync(
            Guid userId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException(
                "Simulación de PostgreSQL inaccesible durante la comprobación administrativa.");
    }
}
