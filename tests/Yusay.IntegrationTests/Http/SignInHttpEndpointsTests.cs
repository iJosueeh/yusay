using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.VerifyEmail;
using Yusay.Application.Identity.Queries.ValidateAccessToken;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Http;

/// <summary>
/// Pruebas HTTP de <c>POST /auth/sign-in</c> en <c>AuthController</c>: credenciales
/// correctas (contrato completo con el JWT emitido), rechazos indistinguibles sin
/// enumeración de cuentas, correo no verificado, cuenta bloqueada, ausencia de campos
/// sensibles en la respuesta y compatibilidad del token con la validación centralizada.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class SignInHttpEndpointsTests
{
    private const string TestJwtSecret =
        "yusay-http-integration-test-secret-0123456789abcdef-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

    private const string WrongPassword = "WrongPassword#2026";

    private const string CredentialsRejectedMessage =
        "El correo electrónico o la contraseña no son válidos.";

    private readonly PostgreSqlFixture _fixture;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;

    public SignInHttpEndpointsTests(PostgreSqlFixture fixture)
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
    }

    [Fact]
    public async Task SignIn_WithValidCredentials_ShouldReturn200WithTheAuthorizedSessionData()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_si_{Guid.NewGuid():N}@yusay.local";
        var userId = await CreateVerifiedUserAsync(email);

        // Act
        var response = await SignInAsync(client, email, ValidPassword);

        // Assert: contrato autorizado completo — identidad y credencial de sesión vigente
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var payload = await response.Content.ReadFromJsonAsync<SignInPayload>();
        Assert.NotNull(payload);
        Assert.Equal(userId, payload.UserId);
        Assert.Equal(email.ToLowerInvariant(), payload.Email);
        Assert.Equal("Bearer", payload.TokenType);
        Assert.Equal(3, payload.AccessToken.Split('.').Length);
        Assert.True(payload.ExpiresAt > payload.IssuedAt);
        Assert.True(payload.IssuedAt <= DateTimeOffset.UtcNow.AddMinutes(1));

        // Assert: auditoría normativa SIGN_IN_SUCCEEDED exactamente una vez para la cuenta
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        var audits = await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM yusay.audit_event WHERE action = 'SIGN_IN_SUCCEEDED' AND actor_user_id = @UserId",
            new { UserId = userId });
        Assert.Equal(1, audits);
    }

    [Fact]
    public async Task SignIn_WithWrongPassword_ShouldReturn401AndAuditTheFailure()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_siw_{Guid.NewGuid():N}@yusay.local";
        await CreateVerifiedUserAsync(email);
        var failuresBefore = await CountSignInFailuresAsync();

        // Act
        var response = await SignInAsync(client, email, WrongPassword);

        // Assert: rechazo genérico de credenciales y auditoría del intento fallido
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, CredentialsRejectedMessage);
        Assert.Equal(failuresBefore + 1, await CountSignInFailuresAsync());
    }

    [Fact]
    public async Task SignIn_WithUnknownEmail_ShouldBeIndistinguishableFromAWrongPassword()
    {
        // Arrange: cuenta existente verificada frente a correo inexistente
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string existingEmail = $"http_sie_{Guid.NewGuid():N}@yusay.local";
        await CreateVerifiedUserAsync(existingEmail);
        string unknownEmail = $"http_sinx_{Guid.NewGuid():N}@yusay.local";

        // Act: misma petición con credencial incorrecta y con usuario inexistente
        var wrongPassword = await SignInAsync(client, existingEmail, WrongPassword);
        var unknownUser = await SignInAsync(client, unknownEmail, ValidPassword);

        // Assert: sin enumeración de cuentas — mismo estado, título y detalle en ambos casos
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownUser.StatusCode);

        var wrongProblem = await wrongPassword.Content.ReadFromJsonAsync<ProblemDetails>();
        var unknownProblem = await unknownUser.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(wrongProblem);
        Assert.NotNull(unknownProblem);
        Assert.Equal(CredentialsRejectedMessage, unknownProblem.Detail);
        Assert.Equal(wrongProblem.Title, unknownProblem.Title);
        Assert.Equal(wrongProblem.Detail, unknownProblem.Detail);
        Assert.Equal(wrongProblem.Status, unknownProblem.Status);
    }

    [Fact]
    public async Task SignIn_WithUnverifiedEmail_ShouldReturn401()
    {
        // Arrange: cuenta creada pero sin verificar el correo
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_siu_{Guid.NewGuid():N}@yusay.local";
        var registration = await CreateRegisterUseCase().ExecuteAsync(
            new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));

        // Act
        var response = await SignInAsync(client, email, ValidPassword);

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized,
            "verificarse antes de iniciar sesión");
        Assert.NotEqual(Guid.Empty, registration.UserId);
    }

    [Fact]
    public async Task SignIn_WithBlockedAccount_ShouldReturn401()
    {
        // Arrange: cuenta verificada bloqueada directamente en el modelo físico
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_sib_{Guid.NewGuid():N}@yusay.local";
        var userId = await CreateVerifiedUserAsync(email);
        await BlockUserAsync(userId);

        // Act
        var response = await SignInAsync(client, email, ValidPassword);

        // Assert: el estado de cuenta se rechaza con la auditoría y el 401 del caso de uso
        await AssertProblemAsync(response, HttpStatusCode.Unauthorized, "La cuenta está bloqueada");
    }

    [Fact]
    public async Task SignIn_Response_ShouldNotExposeSensitiveFields()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_sis_{Guid.NewGuid():N}@yusay.local";
        await CreateVerifiedUserAsync(email);

        // Act
        var response = await SignInAsync(client, email, ValidPassword);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        // Assert: sin credenciales internas ni secretos en el cuerpo
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);

        // Assert: contrato exacto — el cuerpo contiene exclusivamente los campos autorizados
        using var parsed = JsonDocument.Parse(body);
        var propertyNames = parsed.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(
            new[] { "accessToken", "email", "expiresAt", "issuedAt", "tokenType", "userId" },
            propertyNames);
    }

    [Fact]
    public async Task SignIn_IssuedToken_ShouldBeAcceptedByTheCentralizedValidation()
    {
        // Arrange: Redis de la denylist para que la validación centralizada pueda consultarlo
        var redis = new RedisFixture();
        await redis.InitializeAsync();
        try
        {
            using var factory = CreateFactory(redis);
            using var client = factory.CreateClient();
            string email = $"http_sij_{Guid.NewGuid():N}@yusay.local";
            await CreateVerifiedUserAsync(email);

            // Act: token emitido por el endpoint HTTP
            var response = await SignInAsync(client, email, ValidPassword);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var payload = await response.Content.ReadFromJsonAsync<SignInPayload>();
            Assert.NotNull(payload);

            // Assert: el IValidateAccessTokenUseCase real del contenedor acepta el token
            // (firma, denylist, cuenta y política de revocación por cambio de contraseña).
            using var scope = factory.Services.CreateScope();
            var validator = scope.ServiceProvider.GetRequiredService<IValidateAccessTokenUseCase>();
            var validation = await validator.ExecuteAsync(
                new ValidateAccessTokenQuery(payload.AccessToken));

            Assert.Equal(payload.UserId, validation.UserId);
            Assert.Equal(payload.Email, validation.Email);
        }
        finally
        {
            await redis.DisposeAsync();
        }
    }

    [Fact]
    public async Task SignIn_WithMissingPassword_ShouldReturn400ProblemDetails()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/auth/sign-in", new
        {
            email = $"http_siv_{Guid.NewGuid():N}@yusay.local",
            password = string.Empty
        });

        // Assert: validación de entrada con el ProblemDetails centralizado
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "obligatoria");
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Host de prueba de la API en Development con PostgreSQL de la colección y el secreto
    /// JWT de prueba. Cuando se pasa un Redis, también la denylist apunta a ese contenedor
    /// (necesario para ejercitar la validación centralizada del token).
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory(RedisFixture? redis = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.ConnectionString);
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            if (redis is not null)
            {
                builder.UseSetting("REDIS_HOST", redis.Host);
                builder.UseSetting("REDIS_PORT", redis.MappedPort.ToString());
            }
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

    private async Task<Guid> CreateVerifiedUserAsync(string email)
    {
        var registration = await CreateRegisterUseCase()
            .ExecuteAsync(new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));
        await CreateVerifyEmailUseCase()
            .ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));
        return registration.UserId;
    }

    private async Task BlockUserAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "UPDATE yusay.user_account SET status = 'BLOCKED' WHERE user_id = @UserId",
            new { UserId = userId });
    }

    private async Task<long> CountSignInFailuresAsync()
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM yusay.audit_event WHERE action = 'SIGN_IN_FAILED'");
    }

    private static Task<HttpResponseMessage> SignInAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/sign-in", new { email, password });

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
        Assert.Contains(expectedDetailFragment, problem.Detail!, StringComparison.Ordinal);
        Assert.True(problem.Extensions.ContainsKey("traceId"));
    }

    private sealed record SignInPayload(
        Guid UserId,
        string Email,
        string AccessToken,
        string TokenType,
        DateTimeOffset IssuedAt,
        DateTimeOffset ExpiresAt);
}
