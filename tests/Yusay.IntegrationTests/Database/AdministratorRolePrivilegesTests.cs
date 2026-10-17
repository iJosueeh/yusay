using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Yusay.Application.Common.Interfaces;
using Yusay.Application.Identity.Commands.RegisterUser;
using Yusay.Application.Identity.Tokens;
using Yusay.Infrastructure.Audit.Repositories;
using Yusay.Infrastructure.Emailing;
using Yusay.Infrastructure.Identity.Repositories;
using Yusay.Infrastructure.Identity.Services;
using Yusay.Infrastructure.Persistence;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Database;

/// <summary>
/// B8b-1 de RF-021 — mínimo privilegio sobre <c>yusay.administrator</c> verificado con
/// permisos EFECTIVOS: cada operación se ejecuta en conexiones reales como yusay_app,
/// yusay_worker y yusay_admin_ops (consultar GRANT desde postgres no basta), afirmando
/// SQLSTATE 42501 en lo no autorizado y el éxito de las operaciones autorizadas
/// (alta/baja por yusay_admin_ops, lectura por yusay_app). Incluye una ejecución
/// representativa de la API real con <c>DATABASE_URL</c> apuntando a yusay_app —única
/// credencial que recibe el host— cubriendo registro, verificación, sign-in, la política
/// Administrator (SELECT efectivo de yusay_app), escritura del catálogo y lectura pública.
/// La revocación administrativa con el mismo JWT sigue devolviendo 403 sin cambiar JWT ni
/// <c>AdministratorAuthorizationHandler</c> (OQ-ARCH-017): queda cubierta por
/// <c>AdminAuthorizationHttpTests</c> en la misma suite.
/// El procedimiento operacional de alta/baja de administradores y su bitácora quedan
/// pendientes de B8b-2; OQ-ARCH-018, OQ-NFR-004 y hosting siguen OPEN.
/// </summary>
[Collection("DatabaseCollection")]
public sealed class AdministratorRolePrivilegesTests : IAsyncLifetime
{
    private const string TestJwtSecret =
        "yusay-admin-role-privileges-http-test-secret-0123456789abcdef";

    private const string ValidPassword = "NormativePassword#2026";

    private const string AppRole = "yusay_app";
    private const string WorkerRole = "yusay_worker";
    private const string OpsRole = "yusay_admin_ops";

    private readonly PostgreSqlFixture _fixture;
    private readonly RedisFixture _redis = new();
    private readonly UnitOfWork _unitOfWork;
    private readonly UserAccountRepository _userAccountRepo;
    private readonly UserCredentialRepository _userCredentialRepo;
    private readonly EmailVerificationTokenRepository _verificationTokenRepo;
    private readonly AuditEventRepository _auditEventRepo;
    private readonly Argon2idPasswordHasher _passwordHasher;
    private readonly CapturingEmailVerificationSender _emailSender = new();

    public AdministratorRolePrivilegesTests(PostgreSqlFixture fixture)
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
    }

    public async Task InitializeAsync() => await _redis.InitializeAsync();

    public async Task DisposeAsync() => await _redis.DisposeAsync();

    // ------------------------------------------------------------------------------------------
    // 1. Permisos efectivos por conexión real con cada rol (SQLSTATE 42501)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AdministratorTable_WhenAccessedThroughRealRoleConnections_ShouldEnforceLeastPrivilege()
    {
        var userId = await CreateUserAsync();

        // --- yusay_app: SOLO SELECT (requisito de AdministratorAuthorizationHandler) ---
        await using (var app = await OpenRoleConnectionAsync(AppRole))
        {
            Assert.Equal(AppRole, await app.ExecuteScalarAsync<string>("SELECT current_user"));

            Assert.Equal(0, await app.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = userId }));

            await Assert42501Async(() => app.ExecuteAsync(
                "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                new { UserId = userId }));
            await Assert42501Async(() => app.ExecuteAsync(
                "UPDATE yusay.administrator SET user_id = @UserId WHERE user_id = @UserId",
                new { UserId = userId }));
            await Assert42501Async(() => app.ExecuteAsync(
                "DELETE FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = userId }));
        }

        // --- yusay_worker: sin privilegios sobre administrator (sin dependencia legítima;
        //     su purga opera sobre audit_event y los intentos caducados) ---
        await using (var worker = await OpenRoleConnectionAsync(WorkerRole))
        {
            Assert.Equal(WorkerRole, await worker.ExecuteScalarAsync<string>("SELECT current_user"));

            await Assert42501Async(() => worker.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM yusay.administrator"));
            await Assert42501Async(() => worker.ExecuteAsync(
                "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                new { UserId = userId }));
            await Assert42501Async(() => worker.ExecuteAsync(
                "DELETE FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = userId }));
        }

        // --- yusay_admin_ops: SELECT/INSERT/DELETE sin UPDATE ni GRANT OPTION ---
        await using (var ops = await OpenRoleConnectionAsync(OpsRole))
        {
            Assert.Equal(OpsRole, await ops.ExecuteScalarAsync<string>("SELECT current_user"));

            var totalBefore = await CountAdministratorsAsync(ops);

            // Alta autorizada
            Assert.Equal(1, await ops.ExecuteAsync(
                "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                new { UserId = userId }));
            Assert.Equal(1, await ops.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = userId }));

            // UPDATE no concedido
            await Assert42501Async(() => ops.ExecuteAsync(
                "UPDATE yusay.administrator SET user_id = @UserId WHERE user_id = @UserId",
                new { UserId = userId }));

            // Sin GRANT OPTION: PostgreSQL no redistribuye privilegios. Como yusay_admin_ops
            // posee privilegios planos sobre la tabla, el GRANT no arroja error sino que se
            // cumple como no-op con WARNING "no privileges were granted" (solo se otorgan los
            // privilegios con grant option, doc. GRANT de PostgreSQL) — se afirma el RESULTADO:
            // yusay_app no gana INSERT por la intermedia de yusay_admin_ops.
            await ops.ExecuteAsync(
                "GRANT INSERT ON yusay.administrator TO " + AppRole);
            Assert.False(await ops.ExecuteScalarAsync<bool>(
                "SELECT has_table_privilege(@Role, 'yusay.administrator', 'INSERT')",
                new { Role = AppRole }));

            // Baja autorizada
            Assert.Equal(1, await ops.ExecuteAsync(
                "DELETE FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = userId }));
            Assert.Equal(0, await ops.ExecuteScalarAsync<int>(
                "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = userId }));

            // La colección comparte la base: el alta/baja deja intacto el recuento global
            Assert.Equal(totalBefore, await CountAdministratorsAsync(ops));
        }
    }

    // ------------------------------------------------------------------------------------------
    // 2. Ejecución representativa de la API con DATABASE_URL = yusay_app
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Api_WhenConnectedAsYusayAppRole_ShouldRunTheRepresentativeIdentityAndCatalogFlow()
    {
        using var factory = CreateFactoryForAppRole();
        using var client = factory.CreateClient();
        var email = $"b8b1_app_{Guid.NewGuid():N}@yusay.local";

        // Identidad completa escrita por yusay_app (user_account, user_credential,
        // email_verification_token y audit_event); la API no escribe administrator.
        var register = await client.PostAsJsonAsync("/auth/register", new
        {
            email,
            password = ValidPassword,
            adultConfirmed = true
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        Guid userId;
        using (var document = JsonDocument.Parse(await register.Content.ReadAsStringAsync()))
        {
            userId = document.RootElement.GetProperty("id").GetGuid();
        }

        var delivery = Assert.Single(_emailSender.Deliveries);
        var verify = await client.PostAsJsonAsync("/auth/verify-email", new { token = delivery.Token });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var signIn = await client.PostAsJsonAsync("/auth/sign-in", new { email, password = ValidPassword });
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        string accessToken;
        using (var document = JsonDocument.Parse(await signIn.Content.ReadAsStringAsync()))
        {
            accessToken = document.RootElement.GetProperty("accessToken").GetString()
                ?? throw new InvalidOperationException("La respuesta de sign-in no incluyó accessToken.");
        }

        // La fila de administrador se siembra con la conexión de la fixture (superusuario):
        // la API solo debe leerla con su SELECT efectivo de yusay_app.
        await GrantAdministratorAsync(userId);

        var adminList = await SendGetAsync(client, "/admin/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, adminList.StatusCode);

        // Escritura de catálogo como yusay_app (dimension + audit_event)
        var created = await SendJsonAsync(
            client, HttpMethod.Post, "/admin/dimensions",
            JsonContent.Create(new
            {
                code = $"b8b1_app_{Guid.NewGuid():N}",
                name = "B8b-1 rol de aplicación",
                description = "Dimensión creada verificando el mínimo privilegio de yusay_app."
            }),
            accessToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        // Lectura autenticada del catálogo público
        var publicList = await SendGetAsync(client, "/dimensions", accessToken);
        Assert.Equal(HttpStatusCode.OK, publicList.StatusCode);
    }

    // ------------------------------------------------------------------------------------------
    // Composición
    // ------------------------------------------------------------------------------------------

    /// <summary>
    /// Host de prueba de la API con <c>DATABASE_URL</c> autenticándose como yusay_app: el
    /// host solo recibe esa credencial, por lo que toda ejecución ocurre con el rol real.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactoryForAppRole() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("DATABASE_URL", _fixture.BuildRoleConnectionString(AppRole));
            builder.UseSetting("JWT_SECRET", TestJwtSecret);
            builder.UseSetting("REDIS_HOST", _redis.Host);
            builder.UseSetting("REDIS_PORT", _redis.MappedPort.ToString());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailVerificationSender>();
                services.AddSingleton<IEmailVerificationSender>(_emailSender);
            });
        });

    private async Task<Guid> CreateUserAsync()
    {
        string email = $"b8b1_priv_{Guid.NewGuid():N}@yusay.local";
        var registration = await new RegisterUserUseCase(
                _unitOfWork, _userAccountRepo, _userCredentialRepo, _verificationTokenRepo,
                _auditEventRepo, _passwordHasher, new SecureTokenService(), new NullEmailVerificationSender())
            .ExecuteAsync(new RegisterUserCommand(email, ValidPassword, AdultConfirmed: true));
        return registration.UserId;
    }

    private async Task<NpgsqlConnection> OpenRoleConnectionAsync(string role)
    {
        var connection = new NpgsqlConnection(_fixture.BuildRoleConnectionString(role));
        await connection.OpenAsync();
        return connection;
    }

    private static Task<int> CountAdministratorsAsync(NpgsqlConnection connection) =>
        connection.ExecuteScalarAsync<int>("SELECT count(*) FROM yusay.administrator");

    private async Task GrantAdministratorAsync(Guid userId)
    {
        await using var connection = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = userId });
    }

    private static async Task<PostgresException> Assert42501Async(Func<Task> operation)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(operation);
        Assert.Equal("42501", exception.SqlState);
        return exception;
    }

    private static Task<HttpResponseMessage> SendGetAsync(
        HttpClient client, string path, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SendJsonAsync(
        HttpClient client,
        HttpMethod method,
        string path,
        HttpContent content,
        string? accessToken = null)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request);
    }

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
