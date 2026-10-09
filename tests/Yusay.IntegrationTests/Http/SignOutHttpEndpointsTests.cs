using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.SignIn;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Pruebas HTTP de <c>POST /auth/sign-out</c> trasladado a <c>AuthController</c>: contrato
/// 204 del cierre con JWT vigente (mintiado con el mismo secreto que la API), conservación
/// de la auditoría normativa SIGN_OUT y política fail-closed 503 cuando Redis se cae,
/// verificando que la eliminación de los <c>try/catch</c> no ha alterado códigos ni garantías.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class SignOutHttpEndpointsTests : IAsyncLifetime
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

    public SignOutHttpEndpointsTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _unitOfWork = new UnitOfWork(fixture.ConnectionFactory);
        _userAccountRepo = new UserAccountRepository(fixture.ConnectionFactory);
        _userCredentialRepo = new UserCredentialRepository(fixture.ConnectionFactory);
        _verificationTokenRepo = new EmailVerificationTokenRepository(fixture.ConnectionFactory);
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

    // El host se crea dentro de cada test (y no en el ctor) porque necesita el puerto
    // asignado por Docker, que solo existe tras InitializeAsync.
    public async Task InitializeAsync() => await _redis.InitializeAsync();

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    [Fact]
    public async Task SignOut_WithVigentToken_ShouldReturn204AndWriteTheSignOutAudit()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión real (registro + verificación + signIn) mintiada contra la API
        string email = $"http_so_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendSignOutAsync(client, accessToken);

        // Assert: contrato exacto — 204 sin cuerpo
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());

        // Assert end-to-end: el evento normativo SIGN_OUT se escribió en PostgreSQL real
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var signOutAudits = await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM yusay.audit_event WHERE action = 'SIGN_OUT' AND actor_user_id = @UserId",
            new { UserId = userId });
        Assert.Equal(1, signOutAudits);
    }

    [Fact]
    public async Task SignOut_WhenRedisStopsAfterASuccessfulClose_ShouldFailClosedWith503ProblemDetails()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión válida y primer cierre con Redis disponible
        string email = $"http_so503_{Guid.NewGuid():N}@yusay.local";
        var (_, accessToken) = await CreateSessionAsync(email);

        var closed = await SendSignOutAsync(client, accessToken);
        Assert.Equal(HttpStatusCode.NoContent, closed.StatusCode);

        // Act: Redis cae y el cliente pierde la conexión establecida
        await _redis.StopAsync();
        var failed = await SendSignOutAsync(client, accessToken);

        // Assert: fail-closed conservado — 503 ProblemDetails, nunca un 204 engañoso
        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("application/problem+json", failed.Content.Headers.ContentType?.MediaType);
        var problem = await failed.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.Status);
        Assert.Equal("Servicio no disponible", problem.Title);
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Host de prueba de la API apuntando a PostgreSQL de la colección y al Redis propio de
    /// esta clase, con el secreto JWT compartido con el servicio de firmado local.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
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

    private Task<HttpResponseMessage> SendSignOutAsync(HttpClient client, string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/sign-out");
        request.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client.SendAsync(request);
    }
}
