using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yusay.Application.Common.Exceptions;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Commands.RequestPasswordReset;
using Yusay.Application.Identity.Commands.ResetPassword;
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
/// Pruebas HTTP de <c>POST /auth/password-reset/request</c> y <c>POST /auth/password-reset/confirm</c>
/// en <c>AuthController</c>: respuestas indistinguibles sin enumeración, entrega del token
/// exclusivamente por la abstracción de correo, restablecimiento exitoso, rechazos de token
/// inválido/expirado/reutilizado, política de contraseña y revocación global de los JWT
/// anteriores mediante <c>password_changed_at</c>/<c>pwd_at</c>.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class PasswordResetHttpEndpointsTests
{
    private const string TestJwtSecret =
        "yusay-http-integration-test-secret-0123456789abcdef-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

    private const string NewPassword = "RecoveredPassword#2026";

    private const string CredentialsRejectedMessage =
        "El correo electrónico o la contraseña no son válidos.";

    private readonly PostgreSqlFixture _fixture;
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly PasswordResetTokenRepository _resetTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly SecureTokenService _tokenService;

    public PasswordResetHttpEndpointsTests(PostgreSqlFixture fixture)
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
        _tokenService = new SecureTokenService();
    }

    [Fact]
    public async Task PasswordResetRequest_WithExistingEmail_ShouldReturn200AndDeliverTheTokenByEmailAbstraction()
    {
        // Arrange: host con doble que captura la entrega del token de recuperación
        var capturingSender = new CapturingPasswordResetEmailSender();
        using var factory = CreateFactory(capturingSender);
        using var client = factory.CreateClient();
        string email = $"http_prq_{Guid.NewGuid():N}@yusay.local";
        var userId = await CreateVerifiedUserAsync(email);

        // Act
        var response = await client.PostAsJsonAsync("/auth/password-reset/request", new { email });

        // Assert: confirmación genérica 200
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var payload = await response.Content.ReadFromJsonAsync<RequestAcceptedPayload>();
        Assert.NotNull(payload);
        Assert.True(payload.Accepted);

        // Assert: el token salió una sola vez por la abstracción de correo, con la cuenta real
        var delivery = Assert.Single(capturingSender.Deliveries);
        Assert.Equal(email.ToLowerInvariant(), delivery.Email);
        Assert.False(string.IsNullOrWhiteSpace(delivery.Token));

        // Assert: el token no aparece jamás en la respuesta HTTP
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(delivery.Token, body);

        // Assert: auditoría normativa de expedición del token
        Assert.Equal(1, await CountAuditAsync("PASSWORD_RESET_TOKEN_ISSUED", userId));
    }

    [Fact]
    public async Task PasswordResetRequest_WithUnknownEmail_ShouldBeIndistinguishableAndDeliverNothing()
    {
        // Arrange
        var capturingSender = new CapturingPasswordResetEmailSender();
        using var factory = CreateFactory(capturingSender);
        using var client = factory.CreateClient();
        string existingEmail = $"http_prx_{Guid.NewGuid():N}@yusay.local";
        await CreateVerifiedUserAsync(existingEmail);
        string unknownEmail = $"http_prxx_{Guid.NewGuid():N}@yusay.local";

        // Act: misma petición para cuenta existente e inexistente
        var existing = await client.PostAsJsonAsync("/auth/password-reset/request", new { email = existingEmail });
        var unknown = await client.PostAsJsonAsync("/auth/password-reset/request", new { email = unknownEmail });

        // Assert: sin enumeración — mismo estado y mismo cuerpo exactamente
        Assert.Equal(HttpStatusCode.OK, existing.StatusCode);
        Assert.Equal(HttpStatusCode.OK, unknown.StatusCode);
        Assert.Equal(await existing.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());

        // Assert: solo el correo existente recibió entrega; el inexistente no genera envío
        var delivery = Assert.Single(capturingSender.Deliveries);
        Assert.Equal(existingEmail.ToLowerInvariant(), delivery.Email);
    }

    [Fact]
    public async Task PasswordResetResponses_ShouldNotExposeTokensNorSensitiveData()
    {
        // Arrange
        var capturingSender = new CapturingPasswordResetEmailSender();
        using var factory = CreateFactory(capturingSender);
        using var client = factory.CreateClient();
        string email = $"http_prn_{Guid.NewGuid():N}@yusay.local";
        await CreateVerifiedUserAsync(email);

        // Act
        var request = await client.PostAsJsonAsync("/auth/password-reset/request", new { email });
        var resetToken = (await RequestResetTokenAsync(email))!;
        var confirm = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = resetToken,
            newPassword = NewPassword
        });

        // Assert: la solicitud solo confirma la recepción — sin cuenta ni token
        Assert.Equal(HttpStatusCode.OK, request.StatusCode);
        var requestBody = await request.Content.ReadAsStringAsync();
        Assert.DoesNotContain("token", requestBody, StringComparison.OrdinalIgnoreCase);
        using (var parsedRequest = JsonDocument.Parse(requestBody))
        {
            var requestProperties = parsedRequest.RootElement.EnumerateObject()
                .Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal);
            Assert.Equal(new[] { "accepted" }, requestProperties);
        }

        // Assert: la confirmación solo devuelve la identidad pública — sin credenciales
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var confirmBody = await confirm.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", confirmBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", confirmBody, StringComparison.OrdinalIgnoreCase);
        using var parsedConfirm = JsonDocument.Parse(confirmBody);
        var confirmProperties = parsedConfirm.RootElement.EnumerateObject()
            .Select(property => property.Name)
            .OrderBy(name => name, StringComparer.Ordinal);
        Assert.Equal(new[] { "email", "userId" }, confirmProperties);
    }

    [Fact]
    public async Task PasswordResetConfirm_WithValidToken_ShouldReturn200AndAcceptTheNewPassword()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_prc_{Guid.NewGuid():N}@yusay.local";
        var userId = await CreateVerifiedUserAsync(email);
        var resetToken = (await RequestResetTokenAsync(email))!;

        // Act
        var confirm = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = resetToken,
            newPassword = NewPassword
        });

        // Assert: identidad pública y auditoría del cambio
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var payload = await confirm.Content.ReadFromJsonAsync<ResetCompletedPayload>();
        Assert.NotNull(payload);
        Assert.Equal(userId, payload.UserId);
        Assert.Equal(email.ToLowerInvariant(), payload.Email);
        Assert.Equal(1, await CountAuditAsync("PASSWORD_RESET_COMPLETED", userId));

        // Assert: la nueva contraseña autentica de extremo a extremo
        var signIn = await client.PostAsJsonAsync("/auth/sign-in", new
        {
            email,
            password = NewPassword
        });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
    }

    [Fact]
    public async Task PasswordResetConfirm_WithUnknownAndReusedToken_ShouldReturn400ProblemDetails()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_prr_{Guid.NewGuid():N}@yusay.local";
        await CreateVerifiedUserAsync(email);
        var resetToken = (await RequestResetTokenAsync(email))!;

        // Act y Assert: token desconocido
        var unknown = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = $"invalid_{Guid.NewGuid():N}",
            newPassword = NewPassword
        });
        await AssertProblemAsync(unknown, HttpStatusCode.BadRequest,
            "no existe o ya ha sido consumido");

        // Act y Assert: primer consumo correcto y reutilización rechazada con el mismo mensaje
        var first = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = resetToken,
            newPassword = NewPassword
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var reused = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = resetToken,
            newPassword = "AnotherPassword#2026"
        });
        await AssertProblemAsync(reused, HttpStatusCode.BadRequest,
            "no existe o ya ha sido consumido");
    }

    [Fact]
    public async Task PasswordResetConfirm_WithExpiredToken_ShouldReturn400ProblemDetails()
    {
        // Arrange: token válido caducado directamente en el modelo físico
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_pre_{Guid.NewGuid():N}@yusay.local";
        var userId = await CreateVerifiedUserAsync(email);
        var resetToken = (await RequestResetTokenAsync(email))!;
        await using (var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync())
        {
            await connection.ExecuteAsync(
                """
                UPDATE yusay.password_reset_token
                SET created_at = NOW() - interval '45 minutes',
                    expires_at = NOW() - interval '15 minutes'
                WHERE user_id = @UserId
                """,
                new { UserId = userId });
        }

        // Act
        var response = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = resetToken,
            newPassword = NewPassword
        });

        // Assert
        await AssertProblemAsync(response, HttpStatusCode.BadRequest, "ha expirado");
    }

    [Fact]
    public async Task PasswordResetConfirm_WithPolicyViolatingInput_ShouldReturn400ProblemDetails()
    {
        // Arrange
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        string email = $"http_prv_{Guid.NewGuid():N}@yusay.local";
        await CreateVerifiedUserAsync(email);
        var resetToken = (await RequestResetTokenAsync(email))!;

        // Act y Assert: contraseña que incumple la política
        var weakPassword = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = resetToken,
            newPassword = "short"
        });
        await AssertProblemAsync(weakPassword, HttpStatusCode.BadRequest, "al menos 8 caracteres");

        // Act y Assert: token ausente (la validación ocurre antes de consumir el token real)
        var missingToken = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
        {
            token = "",
            newPassword = NewPassword
        });
        await AssertProblemAsync(missingToken, HttpStatusCode.BadRequest, "obligatorio");
    }

    [Fact]
    public async Task PasswordResetConfirm_ShouldRejectThePreviousPasswordAndRevokeThePreviousJwt()
    {
        // Arrange: Redis de la denylist para ejercitar la validación centralizada del JWT
        var redis = new RedisFixture();
        await redis.InitializeAsync();
        try
        {
            using var factory = CreateFactory(redis: redis);
            using var client = factory.CreateClient();
            string email = $"http_prj_{Guid.NewGuid():N}@yusay.local";
            await CreateVerifiedUserAsync(email);

            // Sesión vigente con la contraseña actual y token de recuperación expedido
            var signIn = await client.PostAsJsonAsync("/auth/sign-in", new
            {
                email,
                password = ValidPassword
            });
            Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
            using var signedIn = JsonDocument.Parse(await signIn.Content.ReadAsStringAsync());
            var previousAccessToken = signedIn.RootElement.GetProperty("accessToken").GetString();
            Assert.False(string.IsNullOrWhiteSpace(previousAccessToken));

            var resetToken = (await RequestResetTokenAsync(email))!;

            // Act: restablecimiento por HTTP
            var confirm = await client.PostAsJsonAsync("/auth/password-reset/confirm", new
            {
                token = resetToken,
                newPassword = NewPassword
            });
            Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

            // Assert: la contraseña anterior queda rechazada
            var oldPasswordSignIn = await client.PostAsJsonAsync("/auth/sign-in", new
            {
                email,
                password = ValidPassword
            });
            await AssertProblemAsync(oldPasswordSignIn, HttpStatusCode.Unauthorized,
                CredentialsRejectedMessage);

            // Assert: el JWT anterior queda invalidado por la política global pwd_at
            using var scope = factory.Services.CreateScope();
            var validator = scope.ServiceProvider.GetRequiredService<IValidateAccessTokenUseCase>();
            await Assert.ThrowsAsync<UnauthorizedException>(() =>
                validator.ExecuteAsync(new ValidateAccessTokenQuery(previousAccessToken!)));
        }
        finally
        {
            await redis.DisposeAsync();
        }
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Host de prueba de la API en Development con PostgreSQL de la colección y el secreto
    /// JWT de prueba. Permite sustituir el sender de recuperación por un doble observador y
    /// apuntar la denylist a un Redis concreto cuando la prueba valida JWT.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory(
        IPasswordResetEmailSender? passwordResetSender = null,
        RedisFixture? redis = null) =>
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

            if (passwordResetSender is not null)
            {
                builder.ConfigureServices(services =>
                {
                    services.RemoveAll<IPasswordResetEmailSender>();
                    services.AddSingleton(passwordResetSender);
                });
            }
        });

    private RegisterUserUseCase CreateRegisterUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _userCredentialRepo,
        _verificationTokenRepo,
        _auditEventRepo,
        _passwordHasher,
        _tokenService,
        new NullEmailVerificationSender());

    private VerifyEmailUseCase CreateVerifyEmailUseCase() => new(
        _unitOfWork,
        _userAccountRepo,
        _verificationTokenRepo,
        _auditEventRepo,
        _tokenService);

    private RequestPasswordResetUseCase CreateRequestResetUseCase(
        IPasswordResetEmailSender? passwordResetSender = null) => new(
        _unitOfWork,
        _userAccountRepo,
        _resetTokenRepo,
        _auditEventRepo,
        _tokenService,
        passwordResetSender ?? new NullPasswordResetEmailSender());

    private async Task<Guid> CreateVerifiedUserAsync(string email)
    {
        var registration = await CreateRegisterUseCase()
            .ExecuteAsync(new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));
        await CreateVerifyEmailUseCase()
            .ExecuteAsync(new VerifyEmailCommand(registration.VerificationToken));
        return registration.UserId;
    }

    /// <summary>Expedición del token de recuperación a nivel de caso de uso (arreglo de pruebas).</summary>
    private async Task<string?> RequestResetTokenAsync(string email)
    {
        var result = await CreateRequestResetUseCase()
            .ExecuteAsync(new RequestPasswordResetCommand(email));
        return result.ResetToken;
    }

    private async Task<long> CountAuditAsync(string action, Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        return await connection.ExecuteScalarAsync<long>(
            "SELECT count(*) FROM yusay.audit_event WHERE action = @Action AND actor_user_id = @UserId",
            new { Action = action, UserId = userId });
    }

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

    private sealed record RequestAcceptedPayload(bool Accepted);

    private sealed record ResetCompletedPayload(Guid UserId, string Email);

    /// <summary>
    /// Doble que captura la entrega de tokens de recuperación en lugar del proveedor de correo.
    /// </summary>
    private sealed class CapturingPasswordResetEmailSender : IPasswordResetEmailSender
    {
        private readonly object _sync = new();

        public List<(string Email, string Token)> Deliveries { get; } = new();

        public Task SendPasswordResetTokenAsync(
            string recipientEmail,
            string resetToken,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                Deliveries.Add((recipientEmail, resetToken));
            }

            return Task.CompletedTask;
        }
    }
}
