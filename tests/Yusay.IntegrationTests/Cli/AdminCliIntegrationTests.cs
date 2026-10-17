using Dapper;
using Npgsql;
using Xunit;
using Yusay.AdminCli;
using Yusay.AdminCli.Storage;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Cli;

[Collection("DatabaseCollection")]
public sealed class AdminCliIntegrationTests : IDisposable
{
    private const string OpsRole = "yusay_admin_ops";
    private readonly PostgreSqlFixture _fixture;
    private readonly string _connectionString;
    private readonly string _tempStateDir;

    public AdminCliIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        _connectionString = fixture.BuildRoleConnectionString(OpsRole);
        _tempStateDir = Path.Combine(Path.GetTempPath(), $"yusay_admin_cli_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempStateDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempStateDir))
            {
                Directory.Delete(_tempStateDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task ProvisionCommand_WhenSuccessful_PersistsAdministratorAndUpdatesJournalToSuccess()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var app = CreateApp(stdout, stderr);

        var exitCode = await app.RunAsync(new[]
        {
            "provision",
            "--user", targetUserId.ToString(),
            "--operator", "cli_test_operator",
            "--ticket", "TCK-PROV-001",
            "--state-dir", _tempStateDir
        });

        Assert.Equal(0, exitCode);
        Assert.Contains("[RESULT]", stdout.ToString());
        Assert.Contains("Status: Success", stdout.ToString());

        // Verificación en base de datos
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var isAdmin = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM yusay.administrator WHERE user_id = @UserId)",
            new { UserId = targetUserId });
        Assert.True(isAdmin);

        // Verificación del diario local
        using var journal = new FileOperationJournal(_tempStateDir);
        var files = Directory.GetFiles(_tempStateDir, "operation-*.json");
        Assert.Single(files);

        var record = await journal.GetByEventIdAsync(Guid.Parse(Path.GetFileNameWithoutExtension(files[0]).Replace("operation-", "")));
        Assert.NotNull(record);
        Assert.Equal(LocalOperationStatus.CompletedSuccess, record.Status);
    }

    [Fact]
    public async Task RevokeCommand_WhenSuccessful_RemovesAdministratorAndUpdatesJournal()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();

        // 1. Alta previa
        var stdout1 = new StringWriter();
        var app1 = CreateApp(stdout1, new StringWriter());
        var provExit = await app1.RunAsync(new[]
        {
            "provision",
            "--user", targetUserId.ToString(),
            "--operator", "cli_operator",
            "--state-dir", _tempStateDir
        });
        Assert.Equal(0, provExit);

        // 2. Revocación
        var stdout2 = new StringWriter();
        var app2 = CreateApp(stdout2, new StringWriter());
        var revokeExit = await app2.RunAsync(new[]
        {
            "revoke",
            "--user", targetUserId.ToString(),
            "--operator", "cli_operator",
            "--ticket", "TCK-REVOKE-001",
            "--state-dir", _tempStateDir
        });

        Assert.Equal(0, revokeExit);
        Assert.Contains("Status: Success", stdout2.ToString());

        // Verificación en base de datos
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        var isAdmin = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM yusay.administrator WHERE user_id = @UserId)",
            new { UserId = targetUserId });
        Assert.False(isAdmin);
    }

    [Fact]
    public async Task VerifyCommand_WhenEventConfirmedInDb_ReturnsExitCode0AndUpdatesJournal()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        // Siembra directa en base de datos
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync("INSERT INTO yusay.administrator (user_id) VALUES (@UserId)", new { UserId = targetUserId });
            await conn.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier, ticket_ref)
                  VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'op_verify', 'TCK-V1')",
                new { EventId = eventId, UserId = targetUserId });
        }

        // Siembra de registro local pendiente en diario
        using (var journal = new FileOperationJournal(_tempStateDir))
        {
            await journal.PersistPendingAsync(new LocalOperationRecord
            {
                EventId = eventId,
                TargetUserId = targetUserId,
                Action = "Provision",
                OperatorIdentifier = "op_verify",
                TicketRef = "TCK-V1",
                Status = LocalOperationStatus.VerificationPending,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        // Ejecutar verify
        var stdout = new StringWriter();
        var app = CreateApp(stdout, new StringWriter());
        var exitCode = await app.RunAsync(new[]
        {
            "verify",
            "--event-id", eventId.ToString(),
            "--state-dir", _tempStateDir
        });

        Assert.Equal(0, exitCode);
        Assert.Contains("[VERIFIED SUCCESS]", stdout.ToString());

        // El diario local transiciona a CompletedPreviouslyApplied
        using (var journal = new FileOperationJournal(_tempStateDir))
        {
            var record = await journal.GetByEventIdAsync(eventId);
            Assert.NotNull(record);
            Assert.Equal(LocalOperationStatus.CompletedPreviouslyApplied, record.Status);
        }
    }

    [Fact]
    public async Task VerifyCommand_WhenEventAbsentInDb_ReturnsExitCode2VerificationPendingAndDoesNotRunDml()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        // Registro local en estado pendiente
        using (var journal = new FileOperationJournal(_tempStateDir))
        {
            await journal.PersistPendingAsync(new LocalOperationRecord
            {
                EventId = eventId,
                TargetUserId = targetUserId,
                Action = "Provision",
                OperatorIdentifier = "op_absent",
                Status = LocalOperationStatus.VerificationPending,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        // Ejecutar verify sobre evento ausente en PostgreSQL
        var stderr = new StringWriter();
        var app = CreateApp(new StringWriter(), stderr);
        var exitCode = await app.RunAsync(new[]
        {
            "verify",
            "--event-id", eventId.ToString(),
            "--state-dir", _tempStateDir
        });

        // Debe retornar ExitCode 2 (VerificationPending)
        Assert.Equal(2, exitCode);
        Assert.Contains("[VERIFIED PENDING]", stderr.ToString());

        // Comprobar que NO se ejecutó ningún DML
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var isAdmin = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM yusay.administrator WHERE user_id = @UserId)",
            new { UserId = targetUserId });
        Assert.False(isAdmin);
    }

    [Fact]
    public async Task ListPendingCommand_ShouldPrintNonTerminalOperations()
    {
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();

        using (var journal = new FileOperationJournal(_tempStateDir))
        {
            await journal.PersistPendingAsync(new LocalOperationRecord
            {
                EventId = eventId1,
                TargetUserId = Guid.NewGuid(),
                Action = "Provision",
                OperatorIdentifier = "op_list_1",
                Status = LocalOperationStatus.VerificationPending,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            });

            await journal.PersistPendingAsync(new LocalOperationRecord
            {
                EventId = eventId2,
                TargetUserId = Guid.NewGuid(),
                Action = "Revoke",
                OperatorIdentifier = "op_list_2",
                Status = LocalOperationStatus.PendingExecution,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        var stdout = new StringWriter();
        var app = CreateApp(stdout, new StringWriter());
        var exitCode = await app.RunAsync(new[]
        {
            "list-pending",
            "--state-dir", _tempStateDir
        });

        Assert.Equal(0, exitCode);
        var output = stdout.ToString();
        Assert.Contains(eventId1.ToString(), output);
        Assert.Contains(eventId2.ToString(), output);
    }

    [Fact]
    public async Task MissingCredentials_WhenEnvNotConfigured_ShouldReturnExitCode5WithoutDml()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var app = new AdminCliApp(
            stdout,
            stderr,
            envProvider: _ => null); // Sin variable de entorno

        var exitCode = await app.RunAsync(new[]
        {
            "provision",
            "--user", Guid.NewGuid().ToString(),
            "--operator", "op",
            "--state-dir", _tempStateDir
        });

        Assert.Equal(5, exitCode);
        Assert.Contains("No se configuró la cadena de conexión", stderr.ToString());
    }

    [Fact]
    public async Task Recovery_WhenCrashedAfterCommitBeforeJournalUpdate_VerifyReusesOriginalEventIdConfirmsSuccessAndAvoidsDuplicateDml()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var originalEventId = Guid.NewGuid();
        const string op = "crash_operator";
        const string ticket = "TCK-CRASH-RECOV-999";

        // 1. Simulación del estado tras caída:
        // En PostgreSQL: El COMMIT fue exitoso (administrador insertado y evento V020 registrado)
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();
            await conn.ExecuteAsync(
                "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                new { UserId = targetUserId });

            await conn.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier, ticket_ref)
                  VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', @Op, @Ticket)",
                new { EventId = originalEventId, UserId = targetUserId, Op = op, Ticket = ticket });
        }

        // En diario local: Quedó en PendingExecution porque el proceso cayó antes de UpdateStatusAsync
        using (var journal = new FileOperationJournal(_tempStateDir))
        {
            await journal.PersistPendingAsync(new LocalOperationRecord
            {
                EventId = originalEventId,
                TargetUserId = targetUserId,
                Action = "Provision",
                OperatorIdentifier = op,
                TicketRef = ticket,
                Status = LocalOperationStatus.PendingExecution,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }

        // 2. Ejecutar 'list-pending' para comprobar que se detecta como pendiente de reconciliación
        var listStdout = new StringWriter();
        var listApp = CreateApp(listStdout, new StringWriter());
        var listExit = await listApp.RunAsync(new[] { "list-pending", "--state-dir", _tempStateDir });
        Assert.Equal(0, listExit);
        Assert.Contains(originalEventId.ToString(), listStdout.ToString());

        // 3. Ejecutar 'verify' reutilizando obligatoriamente el event_id original
        var verifyStdout = new StringWriter();
        var verifyStderr = new StringWriter();
        var verifyApp = CreateApp(verifyStdout, verifyStderr);
        var verifyExit = await verifyApp.RunAsync(new[]
        {
            "verify",
            "--event-id", originalEventId.ToString(),
            "--state-dir", _tempStateDir
        });

        // 4. Verificaciones:
        // - Retorna código de salida 0 (confirmado exitosamente contra bitácora PostgreSQL)
        Assert.Equal(0, verifyExit);
        Assert.Contains("[VERIFIED SUCCESS]", verifyStdout.ToString());
        Assert.Contains("Coincidencia plena confirmada", verifyStdout.ToString());

        // - El diario local transicionó a estado terminal CompletedPreviouslyApplied
        using (var journal = new FileOperationJournal(_tempStateDir))
        {
            var record = await journal.GetByEventIdAsync(originalEventId);
            Assert.NotNull(record);
            Assert.Equal(LocalOperationStatus.CompletedPreviouslyApplied, record.Status);

            var pending = await journal.ListPendingAsync();
            Assert.Empty(pending);
        }

        // - Verificación de ausencia de DML duplicado en PostgreSQL:
        await using (var conn = new NpgsqlConnection(_connectionString))
        {
            await conn.OpenAsync();

            var adminCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = targetUserId });
            Assert.Equal(1, adminCount);

            var eventCount = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
                new { EventId = originalEventId });
            Assert.Equal(1, eventCount);

            var totalEventsForUser = await conn.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM yusay.administrative_provisioning_event WHERE target_user_id = @UserId",
                new { UserId = targetUserId });
            Assert.Equal(1, totalEventsForUser);
        }
    }

    private AdminCliApp CreateApp(TextWriter stdout, TextWriter stderr)
    {
        return new AdminCliApp(
            stdout,
            stderr,
            envProvider: key => key == "YUSAY_ADMIN_OPS_CONNECTION_STRING" ? _connectionString : null);
    }

    private async Task<Guid> CreateIsolatedUserAccountAsync()
    {
        var userId = Guid.NewGuid();
        var email = $"b8b3c_iso_{Guid.NewGuid():N}@yusay.local";

        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO yusay.user_account (user_id, email, status, adult_confirmed_at)
              VALUES (@UserId, @Email, 'ACTIVE', clock_timestamp())",
            new { UserId = userId, Email = email });

        return userId;
    }
}
