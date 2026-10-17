using Dapper;
using Npgsql;
using Xunit;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Database;

/// <summary>
/// B8b-3A de RF-021 — Mínimo privilegio, integridad, valores técnicos y atomicidad sobre
/// <c>yusay.administrative_provisioning_event</c> y <c>yusay.administrator</c>
/// (MP-PHYS-014 y MP-PHYS-017 / OQ-ARCH-018 bajo Alternativa R1).
///
/// Verifica con conexiones reales y permisos efectivos:
/// 1. Denegación total (SQLSTATE 42501) sobre yusay_app, yusay_worker y PUBLIC.
/// 2. Concesión mínima para yusay_admin_ops: SELECT global e INSERT acotado por columna
///    (event_id, target_user_id, action, operator_identifier, ticket_ref).
/// 3. Ausencia efectiva de INSERT global directo o heredado sobre la tabla para yusay_admin_ops.
/// 4. Protección contra manipulación de columnas técnicas (db_user, client_addr, occurred_at).
/// 5. Restricciones CHECK de acción normativa ('ADMINISTRATOR_PROVISIONED', 'ADMINISTRATOR_REVOKED')
///    y de operator_identifier no vacío.
/// 6. Generación técnica de CURRENT_USER, inet_client_addr() y clock_timestamp() por PostgreSQL.
/// 7. Atomicidad transaccional del INSERT/DELETE en administrator junto al INSERT del evento (MP-PHYS-017).
/// 8. Naturaleza desacoplada de target_user_id (referencia sin FK hacia user_account).
/// </summary>
[Collection("DatabaseCollection")]
public sealed class AdministrativeProvisioningEventPrivilegesTests
{
    private const string AppRole = "yusay_app";
    private const string WorkerRole = "yusay_worker";
    private const string OpsRole = "yusay_admin_ops";

    private const string ActionProvisioned = "ADMINISTRATOR_PROVISIONED";
    private const string ActionRevoked = "ADMINISTRATOR_REVOKED";

    private readonly PostgreSqlFixture _fixture;

    public AdministrativeProvisioningEventPrivilegesTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    // ------------------------------------------------------------------------------------------
    // 1. Permisos efectivos: yusay_app (Acceso revocado en su totalidad)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AppRole_WhenAccessingProvisioningEvent_ShouldDenyAllOperationsWith42501()
    {
        await using var app = await OpenRoleConnectionAsync(AppRole);
        Assert.Equal(AppRole, await app.ExecuteScalarAsync<string>("SELECT current_user"));

        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await Assert42501Async(() => app.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event"));

        await Assert42501Async(() => app.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier)
              VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'test_op')",
            new { EventId = eventId, UserId = userId }));

        await Assert42501Async(() => app.ExecuteAsync(
            "UPDATE yusay.administrative_provisioning_event SET action = 'ADMINISTRATOR_REVOKED' WHERE event_id = @EventId",
            new { EventId = eventId }));

        await Assert42501Async(() => app.ExecuteAsync(
            "DELETE FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId }));
    }

    // ------------------------------------------------------------------------------------------
    // 2. Permisos efectivos: yusay_worker (Acceso revocado en su totalidad)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task WorkerRole_WhenAccessingProvisioningEvent_ShouldDenyAllOperationsWith42501()
    {
        await using var worker = await OpenRoleConnectionAsync(WorkerRole);
        Assert.Equal(WorkerRole, await worker.ExecuteScalarAsync<string>("SELECT current_user"));

        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await Assert42501Async(() => worker.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event"));

        await Assert42501Async(() => worker.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier)
              VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'test_op')",
            new { EventId = eventId, UserId = userId }));

        await Assert42501Async(() => worker.ExecuteAsync(
            "UPDATE yusay.administrative_provisioning_event SET action = 'ADMINISTRATOR_REVOKED' WHERE event_id = @EventId",
            new { EventId = eventId }));

        await Assert42501Async(() => worker.ExecuteAsync(
            "DELETE FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId }));
    }

    // ------------------------------------------------------------------------------------------
    // 3. Ausencia de privilegios para PUBLIC
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task PublicRole_ShouldHaveZeroPrivilegesOnProvisioningEvent()
    {
        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();

        var hasSelect = await conn.ExecuteScalarAsync<bool>(
            "SELECT has_table_privilege('public', 'yusay.administrative_provisioning_event', 'SELECT')");
        var hasInsert = await conn.ExecuteScalarAsync<bool>(
            "SELECT has_table_privilege('public', 'yusay.administrative_provisioning_event', 'INSERT')");
        var hasUpdate = await conn.ExecuteScalarAsync<bool>(
            "SELECT has_table_privilege('public', 'yusay.administrative_provisioning_event', 'UPDATE')");
        var hasDelete = await conn.ExecuteScalarAsync<bool>(
            "SELECT has_table_privilege('public', 'yusay.administrative_provisioning_event', 'DELETE')");

        Assert.False(hasSelect);
        Assert.False(hasInsert);
        Assert.False(hasUpdate);
        Assert.False(hasDelete);
    }

    // ------------------------------------------------------------------------------------------
    // 4. yusay_admin_ops: Ausencia de INSERT global directo/heredado y protección de columnas técnicas
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AdminOpsRole_ShouldHaveColumnLevelInsertOnly_AndNoTableLevelInsert()
    {
        await using var ops = await OpenRoleConnectionAsync(OpsRole);
        Assert.Equal(OpsRole, await ops.ExecuteScalarAsync<string>("SELECT current_user"));

        // Comprobación de privilegio de tabla para el rol específico (evalúa privilegios directos y heredados)
        var hasTableInsertByRole = await ops.ExecuteScalarAsync<bool>(
            "SELECT has_table_privilege('yusay_admin_ops', 'yusay.administrative_provisioning_event', 'INSERT')");
        Assert.False(hasTableInsertByRole, "yusay_admin_ops NO debe tener privilegio INSERT a nivel de tabla.");

        // Comprobación sobre el usuario de sesión activo (CURRENT_USER)
        var hasTableInsertCurrent = await ops.ExecuteScalarAsync<bool>(
            "SELECT has_table_privilege(current_user, 'yusay.administrative_provisioning_event', 'INSERT')");
        Assert.False(hasTableInsertCurrent, "CURRENT_USER (yusay_admin_ops) NO debe poseer INSERT a nivel de tabla.");

        // Comprobación directa en catálogo de information_schema: cero concesiones INSERT a nivel de tabla
        var tableGrantsCount = await ops.ExecuteScalarAsync<int>(
            @"SELECT count(*)
              FROM information_schema.table_privileges
              WHERE table_schema = 'yusay'
                AND table_name = 'administrative_provisioning_event'
                AND privilege_type = 'INSERT'
                AND grantee IN ('yusay_admin_ops', 'PUBLIC')");
        Assert.Equal(0, tableGrantsCount);

        // Columnas técnicas protegidas: NO deben tener permiso de inserción (has_column_privilege = false)
        foreach (var col in new[] { "db_user", "client_addr", "occurred_at" })
        {
            var hasColInsert = await ops.ExecuteScalarAsync<bool>(
                "SELECT has_column_privilege('yusay_admin_ops', 'yusay.administrative_provisioning_event', @Col, 'INSERT')",
                new { Col = col });
            Assert.False(hasColInsert, $"Columna técnica '{col}' NO debe tener permiso de inserción para yusay_admin_ops.");
        }

        // Columnas de negocio autorizadas: SÍ deben tener permiso de inserción (has_column_privilege = true)
        foreach (var col in new[] { "event_id", "target_user_id", "action", "operator_identifier", "ticket_ref" })
        {
            var hasColInsert = await ops.ExecuteScalarAsync<bool>(
                "SELECT has_column_privilege('yusay_admin_ops', 'yusay.administrative_provisioning_event', @Col, 'INSERT')",
                new { Col = col });
            Assert.True(hasColInsert, $"Columna de negocio '{col}' debe tener permiso de inserción para yusay_admin_ops.");
        }

        // DML sin lista de columnas (requiere INSERT a nivel de tabla): debe fallar con 42501
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await Assert42501Async(() => ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'op', 'ticket', 'fake_user', '127.0.0.1'::inet, now())",
            new { EventId = eventId, UserId = userId }));

        // DML intentando suministrar explícitamente db_user: debe fallar con 42501
        await Assert42501Async(() => ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier, db_user)
              VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'op', 'fake_user')",
            new { EventId = eventId, UserId = userId }));

        // DML intentando suministrar explícitamente occurred_at: debe fallar con 42501
        await Assert42501Async(() => ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier, occurred_at)
              VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'op', now())",
            new { EventId = eventId, UserId = userId }));

        // DML intentando suministrar explícitamente client_addr: debe fallar con 42501
        await Assert42501Async(() => ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier, client_addr)
              VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'op', '10.0.0.1'::inet)",
            new { EventId = eventId, UserId = userId }));

        // Inmutabilidad física: UPDATE y DELETE deben fallar con 42501
        await Assert42501Async(() => ops.ExecuteAsync(
            "UPDATE yusay.administrative_provisioning_event SET action = 'ADMINISTRATOR_REVOKED' WHERE event_id = @EventId",
            new { EventId = eventId }));

        await Assert42501Async(() => ops.ExecuteAsync(
            "DELETE FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId }));
    }

    // ------------------------------------------------------------------------------------------
    // 5. Inserción autorizada, valores DEFAULT del motor e integridad de restricciones
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AdminOpsRole_AuthorizedInsert_ShouldGenerateTechnicalDefaultsAndRespectIntegrity()
    {
        await using var ops = await OpenRoleConnectionAsync(OpsRole);

        var eventId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var ticketRef = "SEC-TICKET-2026";
        var beforeTime = DateTimeOffset.UtcNow;

        // Inserción válida únicamente con las 5 columnas autorizadas
        var rows = await ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier, ticket_ref)
              VALUES (@EventId, @UserId, @Action, 'ops_runner_agent', @TicketRef)",
            new { EventId = eventId, UserId = targetUserId, Action = ActionProvisioned, TicketRef = ticketRef });

        Assert.Equal(1, rows);

        // Lectura y verificación de los valores técnicos generados automáticamente por PostgreSQL
        var record = await ops.QuerySingleAsync<(
            Guid event_id,
            Guid target_user_id,
            string action,
            string operator_identifier,
            string ticket_ref,
            string db_user,
            DateTimeOffset occurred_at
        )>(
            @"SELECT event_id, target_user_id, action, operator_identifier, ticket_ref, db_user, occurred_at
              FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });

        Assert.Equal(eventId, record.event_id);
        Assert.Equal(targetUserId, record.target_user_id);
        Assert.Equal(ActionProvisioned, record.action);
        Assert.Equal("ops_runner_agent", record.operator_identifier);
        Assert.Equal(ticketRef, record.ticket_ref);

        // db_user forzado por CURRENT_USER
        Assert.Equal(OpsRole, record.db_user);

        // occurred_at forzado por clock_timestamp() dentro del margen de prueba
        Assert.True(record.occurred_at >= beforeTime.AddSeconds(-2));
        Assert.True(record.occurred_at <= DateTimeOffset.UtcNow.AddSeconds(2));

        // Verificación de Clave Primaria (Idempotencia / Violación 23505)
        var pkEx = await Assert.ThrowsAsync<PostgresException>(() => ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier)
              VALUES (@EventId, @UserId, @Action, 'ops_runner_agent')",
            new { EventId = eventId, UserId = targetUserId, Action = ActionRevoked }));
        Assert.Equal("23505", pkEx.SqlState);
        Assert.Equal("pk_administrative_provisioning_event", pkEx.ConstraintName);

        // Verificación de CHECK: acción inválida (código 23514)
        var actionEx = await Assert.ThrowsAsync<PostgresException>(() => ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier)
              VALUES (@EventId, @UserId, 'INVALID_ACTION', 'ops_runner_agent')",
            new { EventId = Guid.NewGuid(), UserId = targetUserId }));
        Assert.Equal("23514", actionEx.SqlState);
        Assert.Equal("ck_administrative_provisioning_event_action", actionEx.ConstraintName);

        // Verificación de CHECK: operator_identifier vacío (código 23514)
        var opEx = await Assert.ThrowsAsync<PostgresException>(() => ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier)
              VALUES (@EventId, @UserId, @Action, '   ')",
            new { EventId = Guid.NewGuid(), UserId = targetUserId, Action = ActionProvisioned }));
        Assert.Equal("23514", opEx.SqlState);
        Assert.Equal("ck_administrative_provisioning_event_operator_not_empty", opEx.ConstraintName);
    }

    // ------------------------------------------------------------------------------------------
    // 6. Rollback transaccional individual de la bitácora
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task TransactionRollback_ShouldNotPersistProvisioningEvent()
    {
        await using var ops = await OpenRoleConnectionAsync(OpsRole);
        var eventId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        await using (var tx = await ops.BeginTransactionAsync())
        {
            await ops.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier)
                  VALUES (@EventId, @UserId, @Action, 'ops_runner_agent')",
                new { EventId = eventId, UserId = targetUserId, Action = ActionProvisioned }, tx);

            await tx.RollbackAsync();
        }

        var count = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });

        Assert.Equal(0, count);
    }

    // ------------------------------------------------------------------------------------------
    // 7. Referencia desacoplada (target_user_id sin FK física restrictiva hacia user_account)
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task TargetUserId_WithoutFk_AllowsRecordingForNonExistentUserWithoutForeignKeyViolation()
    {
        await using var ops = await OpenRoleConnectionAsync(OpsRole);
        var eventId = Guid.NewGuid();
        var unpersistedUserId = Guid.NewGuid(); // ID inexistente en user_account

        // La inserción en la bitácora no falla por FK porque target_user_id está desacoplado
        var rows = await ops.ExecuteAsync(
            @"INSERT INTO yusay.administrative_provisioning_event
              (event_id, target_user_id, action, operator_identifier)
              VALUES (@EventId, @UserId, @Action, 'ops_runner_agent')",
            new { EventId = eventId, UserId = unpersistedUserId, Action = ActionRevoked });

        Assert.Equal(1, rows);
    }

    // ------------------------------------------------------------------------------------------
    // 8. Atomicidad transaccional de Alta y Baja según MP-PHYS-017
    // ------------------------------------------------------------------------------------------

    [Fact]
    public async Task AtomicProvisioning_Alta_WhenCommitted_ShouldPersistAdministratorAndEvent()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();
        var ticketRef = "CHG-PROV-ALTA-001";

        await using var ops = await OpenRoleConnectionAsync(OpsRole);

        // Protocolo transaccional MP-PHYS-017 para ALTA:
        // 1. BEGIN
        // 2. INSERT INTO yusay.administrator (user_id) VALUES (...) -> RowsAffected == 1
        // 3. INSERT INTO yusay.administrative_provisioning_event (...) -> RowsAffected == 1
        // 4. COMMIT
        await using (var tx = await ops.BeginTransactionAsync())
        {
            var adminRows = await ops.ExecuteAsync(
                "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                new { UserId = targetUserId }, tx);
            Assert.Equal(1, adminRows);

            var eventRows = await ops.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier, ticket_ref)
                  VALUES (@EventId, @UserId, @Action, 'ops_runner', @TicketRef)",
                new { EventId = eventId, UserId = targetUserId, Action = ActionProvisioned, TicketRef = ticketRef }, tx);
            Assert.Equal(1, eventRows);

            await tx.CommitAsync();
        }

        // Verificación post-commit: ambos registros persisten
        var adminCount = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = targetUserId });
        Assert.Equal(1, adminCount);

        var eventRecord = await ops.QuerySingleAsync<(string action, string ticket_ref)>(
            "SELECT action, ticket_ref FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(ActionProvisioned, eventRecord.action);
        Assert.Equal(ticketRef, eventRecord.ticket_ref);
    }

    [Fact]
    public async Task AtomicProvisioning_Alta_WhenRolledBack_ShouldNotPersistAdministratorNorEvent()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        await using var ops = await OpenRoleConnectionAsync(OpsRole);

        await using (var tx = await ops.BeginTransactionAsync())
        {
            var adminRows = await ops.ExecuteAsync(
                "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                new { UserId = targetUserId }, tx);
            Assert.Equal(1, adminRows);

            // Simulación de fallo en inserción de evento (acción inválida que dispara CHECK)
            var ex = await Assert.ThrowsAsync<PostgresException>(() => ops.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier)
                  VALUES (@EventId, @UserId, 'INVALID_ACTION', 'ops_runner')",
                new { EventId = eventId, UserId = targetUserId }, tx));
            Assert.Equal("23514", ex.SqlState);

            await tx.RollbackAsync();
        }

        // Verificación de atomicidad: ni administrator ni provisioning_event persisten
        var adminCount = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = targetUserId });
        Assert.Equal(0, adminCount);

        var eventCount = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task AtomicProvisioning_Baja_WhenCommitted_ShouldRemoveAdministratorAndPersistRevocationEvent()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();
        var ticketRef = "CHG-REVOKE-BAJA-001";

        await using var ops = await OpenRoleConnectionAsync(OpsRole);

        // Pre-condición: el usuario es administrador activo
        await ops.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = targetUserId });

        // Protocolo transaccional MP-PHYS-017 para BAJA:
        // 1. BEGIN
        // 2. DELETE FROM yusay.administrator WHERE user_id = :target_user_id -> RowsAffected == 1
        // 3. INSERT INTO yusay.administrative_provisioning_event (action = 'ADMINISTRATOR_REVOKED') -> RowsAffected == 1
        // 4. COMMIT
        await using (var tx = await ops.BeginTransactionAsync())
        {
            var deletedRows = await ops.ExecuteAsync(
                "DELETE FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = targetUserId }, tx);
            Assert.Equal(1, deletedRows);

            var eventRows = await ops.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier, ticket_ref)
                  VALUES (@EventId, @UserId, @Action, 'ops_runner', @TicketRef)",
                new { EventId = eventId, UserId = targetUserId, Action = ActionRevoked, TicketRef = ticketRef }, tx);
            Assert.Equal(1, eventRows);

            await tx.CommitAsync();
        }

        // Verificación post-commit: administrator ya no existe y evento queda registrado
        var adminCount = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = targetUserId });
        Assert.Equal(0, adminCount);

        var eventRecord = await ops.QuerySingleAsync<(string action, string ticket_ref)>(
            "SELECT action, ticket_ref FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(ActionRevoked, eventRecord.action);
        Assert.Equal(ticketRef, eventRecord.ticket_ref);
    }

    [Fact]
    public async Task AtomicProvisioning_Baja_WhenRolledBack_ShouldPreserveAdministratorAndNotPersistEvent()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        await using var ops = await OpenRoleConnectionAsync(OpsRole);

        // Pre-condición: el usuario es administrador activo
        await ops.ExecuteAsync(
            "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
            new { UserId = targetUserId });

        await using (var tx = await ops.BeginTransactionAsync())
        {
            var deletedRows = await ops.ExecuteAsync(
                "DELETE FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = targetUserId }, tx);
            Assert.Equal(1, deletedRows);

            // Simulación de fallo en inserción del evento de revocación
            var ex = await Assert.ThrowsAsync<PostgresException>(() => ops.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier)
                  VALUES (@EventId, @UserId, 'INVALID_ACTION', 'ops_runner')",
                new { EventId = eventId, UserId = targetUserId }, tx));
            Assert.Equal("23514", ex.SqlState);

            await tx.RollbackAsync();
        }

        // Verificación de atomicidad: el usuario permanece como administrador y no hay evento
        var adminCount = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = targetUserId });
        Assert.Equal(1, adminCount);

        var eventCount = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task AtomicProvisioning_Baja_WhenTargetNotAdministrator_ShouldRollbackAndNotRecordEvent()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        await using var ops = await OpenRoleConnectionAsync(OpsRole);

        // Usuario objetivo NO es administrador; RowsAffected == 0
        await using (var tx = await ops.BeginTransactionAsync())
        {
            var deletedRows = await ops.ExecuteAsync(
                "DELETE FROM yusay.administrator WHERE user_id = @UserId",
                new { UserId = targetUserId }, tx);

            // Regla MP-PHYS-017: Si RowsAffected == 0, abortar con ROLLBACK y no emitir evento
            Assert.Equal(0, deletedRows);
            await tx.RollbackAsync();
        }

        var eventCount = await ops.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(0, eventCount);
    }

    // ------------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------------

    private async Task<Guid> CreateIsolatedUserAccountAsync()
    {
        var userId = Guid.NewGuid();
        var email = $"b8b3a_iso_{Guid.NewGuid():N}@yusay.local";

        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO yusay.user_account (user_id, email, status, adult_confirmed_at)
              VALUES (@UserId, @Email, 'ACTIVE', clock_timestamp())",
            new { UserId = userId, Email = email });

        return userId;
    }

    private async Task<NpgsqlConnection> OpenRoleConnectionAsync(string role)
    {
        var connection = new NpgsqlConnection(_fixture.BuildRoleConnectionString(role));
        await connection.OpenAsync();
        return connection;
    }

    private static async Task<PostgresException> Assert42501Async(Func<Task> operation)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(operation);
        Assert.Equal("42501", exception.SqlState);
        return exception;
    }
}
