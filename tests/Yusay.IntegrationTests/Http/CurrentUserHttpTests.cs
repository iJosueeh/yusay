using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
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
/// Pruebas HTTP de <c>ICurrentUser</c> (F1 de N1, OQ-ARCH-017): la sonda del ensamblado de
/// pruebas expone la identidad corriente en un endpoint protegido con JWT válido (coincide
/// con el usuario autenticado) y en un endpoint anónimo (null, también con cabecera basura).
/// La identidad procede exclusivamente del principal validado —sin segunda validación JWT ni
/// consulta adicional a Redis— y la firma del adaptador lo garantiza estructuralmente.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class CurrentUserHttpTests : IAsyncLifetime
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

    public CurrentUserHttpTests(PostgreSqlFixture fixture)
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

    // Redis propio por prueba (patrón de BearerAuthenticationHttpTests).
    public async Task InitializeAsync() => await _redis.InitializeAsync();

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    [Fact]
    public async Task ProtectedEndpoint_WithValidJwt_ShouldExposeTheAuthenticatedUserId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Arrange: sesión real (registro + verificación + signIn)
        string email = $"http_current_user_{Guid.NewGuid():N}@yusay.local";
        var (userId, accessToken) = await CreateSessionAsync(email);

        // Act
        var response = await SendGetAsync(client, "/test/current-user/protected", accessToken);

        // Assert: ICurrentUser devuelve exactamente la identidad autenticada por el pipeline
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.String, body.RootElement.GetProperty("userId").ValueKind);
        Assert.Equal(userId, body.RootElement.GetProperty("userId").GetGuid());
    }

    [Fact]
    public async Task AnonymousEndpoint_ShouldExposeNullUserId()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act y Assert: sin cabecera de autorización no existe identidad corriente
        var anonymous = await SendGetAsync(client, "/test/current-user/anonymous");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        using var body = JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("userId").ValueKind);

        // La cabecera Authorization basura se ignora en endpoints públicos: la identidad
        // corriente sigue sin existir porque el handler nunca corre (sin IAuthorizeData)
        var withGarbageToken = await SendGetAsync(client, "/test/current-user/anonymous", "garbage-token");
        Assert.Equal(HttpStatusCode.OK, withGarbageToken.StatusCode);
        using var garbageBody = JsonDocument.Parse(await withGarbageToken.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, garbageBody.RootElement.GetProperty("userId").ValueKind);
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Host de prueba de la API con la sonda de ICurrentUser añadida SOLO a este host (la API
    /// real no la incluye ni sus rutas aparecen en el documento OpenAPI).
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
                    .AddApplicationPart(typeof(CurrentUserProbeController).Assembly);
            });
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
