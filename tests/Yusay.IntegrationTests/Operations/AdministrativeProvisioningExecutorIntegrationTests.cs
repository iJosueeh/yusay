using Dapper;
using Npgsql;
using Xunit;
using Yusay.AdminOperations;
using Yusay.AdminOperations.Contracts;
using Yusay.IntegrationTests.Infrastructure;

namespace Yusay.IntegrationTests.Operations;

[Collection("DatabaseCollection")]
public sealed class AdministrativeProvisioningExecutorIntegrationTests
{
    private const string OpsRole = "yusay_admin_ops";
    private readonly PostgreSqlFixture _fixture;
    private readonly IAdminOpsConnectionFactory _connectionFactory;
    private readonly AdministrativeProvisioningExecutor _executor;

    public AdministrativeProvisioningExecutorIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
        var connectionString = fixture.BuildRoleConnectionString(OpsRole);
        _connectionFactory = new AdminOpsConnectionFactory(connectionString);
        _executor = new AdministrativeProvisioningExecutor(_connectionFactory);
    }

    [Fact]
    public async Task Provision_WhenNormalFlow_ShouldSucceedAndPersistBothRecords()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();
        var request = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent",
            "TICKET-ALTA-101");

        var result = await _executor.ExecuteAsync(request);

        Assert.Equal(AdministrativeProvisioningStatus.Success, result.Status);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OccurredAt);

        // Verificación en base de datos
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var isAdmin = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM yusay.administrator WHERE user_id = @UserId)",
            new { UserId = targetUserId });
        Assert.True(isAdmin);

        var eventCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task Provision_WhenSameEventIdReexecutedWithIdenticalPayload_ShouldReturnPreviouslyApplied()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();
        var request = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent",
            "TICKET-ALTA-102");

        // Primera ejecución
        var firstResult = await _executor.ExecuteAsync(request);
        Assert.Equal(AdministrativeProvisioningStatus.Success, firstResult.Status);

        // Segunda ejecución con mismo EventId e idéntico payload (idempotencia)
        var secondResult = await _executor.ExecuteAsync(request);
        Assert.Equal(AdministrativeProvisioningStatus.PreviouslyApplied, secondResult.Status);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.OccurredAt, secondResult.OccurredAt);
    }

    [Fact]
    public async Task Provision_WhenSameEventIdReexecutedWithDifferentPayload_ShouldReturnCollisionConflict()
    {
        var targetUserId1 = await CreateIsolatedUserAccountAsync();
        var targetUserId2 = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        var request1 = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId1,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent",
            "TICKET-1");

        var firstResult = await _executor.ExecuteAsync(request1);
        Assert.Equal(AdministrativeProvisioningStatus.Success, firstResult.Status);

        // Reintento con mismo EventId pero diferente TargetUserId
        var request2 = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId2,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent",
            "TICKET-1");

        var conflictResult = await _executor.ExecuteAsync(request2);
        Assert.Equal(AdministrativeProvisioningStatus.CollisionConflict, conflictResult.Status);
        Assert.False(conflictResult.IsSuccess);
    }

    [Fact]
    public async Task Provision_WhenTargetAlreadyAdministrator_ShouldReturnPreconditionFailed()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var firstEventId = Guid.NewGuid();

        // Alta inicial
        var initialRequest = new AdministrativeProvisioningRequest(
            firstEventId,
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent");
        var firstResult = await _executor.ExecuteAsync(initialRequest);
        Assert.Equal(AdministrativeProvisioningStatus.Success, firstResult.Status);

        // Segundo intento de alta con un NUEVO EventId para el mismo usuario
        var newEventId = Guid.NewGuid();
        var duplicateRequest = new AdministrativeProvisioningRequest(
            newEventId,
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent");

        var result = await _executor.ExecuteAsync(duplicateRequest);
        Assert.Equal(AdministrativeProvisioningStatus.PreconditionFailed, result.Status);
        Assert.False(result.IsSuccess);

        // Confirma que no se registró un evento falso con el nuevo EventId
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = newEventId });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Revoke_WhenNormalFlow_ShouldSucceedAndRemoveAdministrator()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();

        // Pre-condición: el usuario es administrador
        await _executor.ExecuteAsync(new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent"));

        var revokeEventId = Guid.NewGuid();
        var revokeRequest = new AdministrativeProvisioningRequest(
            revokeEventId,
            targetUserId,
            AdministrativeProvisioningAction.Revoke,
            "ops_runner_agent",
            "TICKET-BAJA-201");

        var result = await _executor.ExecuteAsync(revokeRequest);

        Assert.Equal(AdministrativeProvisioningStatus.Success, result.Status);
        Assert.True(result.IsSuccess);

        // Verificación en base de datos
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var isAdmin = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM yusay.administrator WHERE user_id = @UserId)",
            new { UserId = targetUserId });
        Assert.False(isAdmin);

        var eventCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = revokeEventId });
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task Revoke_WhenSameEventIdReexecuted_ShouldReturnPreviouslyApplied()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();

        await _executor.ExecuteAsync(new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent"));

        var revokeEventId = Guid.NewGuid();
        var revokeRequest = new AdministrativeProvisioningRequest(
            revokeEventId,
            targetUserId,
            AdministrativeProvisioningAction.Revoke,
            "ops_runner_agent",
            "TICKET-BAJA-202");

        var firstResult = await _executor.ExecuteAsync(revokeRequest);
        Assert.Equal(AdministrativeProvisioningStatus.Success, firstResult.Status);

        var secondResult = await _executor.ExecuteAsync(revokeRequest);
        Assert.Equal(AdministrativeProvisioningStatus.PreviouslyApplied, secondResult.Status);
        Assert.True(secondResult.IsSuccess);
    }

    [Fact]
    public async Task Revoke_WhenTargetNotAdministrator_ShouldReturnPreconditionFailedAndNotRecordEvent()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        var revokeRequest = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId,
            AdministrativeProvisioningAction.Revoke,
            "ops_runner_agent",
            "TICKET-BAJA-203");

        var result = await _executor.ExecuteAsync(revokeRequest);

        Assert.Equal(AdministrativeProvisioningStatus.PreconditionFailed, result.Status);
        Assert.False(result.IsSuccess);

        // Sin evento falso en la bitácora
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var count = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Concurrency_WhenMultipleDistinctProvisionsRunConcurrently_ShouldAllSucceed()
    {
        var tasks = Enumerable.Range(0, 5).Select(async i =>
        {
            var userId = await CreateIsolatedUserAccountAsync();
            var eventId = Guid.NewGuid();
            var req = new AdministrativeProvisioningRequest(
                eventId,
                userId,
                AdministrativeProvisioningAction.Provision,
                $"concurrency_op_{i}",
                $"TICKET-CONC-{i}");

            return await _executor.ExecuteAsync(req);
        });

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.Equal(AdministrativeProvisioningStatus.Success, r.Status));
    }

    [Fact]
    public async Task Revalidation_WhenDatabaseIsUnreachableAfterFailure_ShouldReturnVerificationPending()
    {
        // Factoría que apunta a un puerto inexistente simulando motor inalcanzable
        var deadFactory = new AdminOpsConnectionFactory("Host=127.0.0.1;Port=59999;Database=fake;Username=fake;Password=fake;Timeout=1;Command Timeout=1;");
        var deadExecutor = new AdministrativeProvisioningExecutor(deadFactory);

        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdministrativeProvisioningAction.Provision,
            "ops_runner");

        var result = await deadExecutor.ExecuteAsync(request);

        // Al no poder determinarse el estado por fallo de conexión, debe emitir VerificationPending
        Assert.Equal(AdministrativeProvisioningStatus.VerificationPending, result.Status);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task CommitIndeterminate_WhenEventAlreadyCommitted_RevalidationReturnsPreviouslyApplied()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();
        var ticketRef = "TICKET-COMMIT-INDETERMINATE";

        // Simula que la transacción consolidó el commit en PostgreSQL,
        // pero la respuesta de red se interrumpió antes de que el cliente la recibiera
        await using (var conn = await _connectionFactory.CreateOpenConnectionAsync())
        {
            await conn.ExecuteAsync(
                "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                new { UserId = targetUserId });

            await conn.ExecuteAsync(
                @"INSERT INTO yusay.administrative_provisioning_event
                  (event_id, target_user_id, action, operator_identifier, ticket_ref)
                  VALUES (@EventId, @UserId, 'ADMINISTRATOR_PROVISIONED', 'ops_runner_agent', @TicketRef)",
                new { EventId = eventId, UserId = targetUserId, TicketRef = ticketRef });
        }

        // El runner reintenta con el mismo EventId conservado
        var request = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner_agent",
            ticketRef);

        var result = await _executor.ExecuteAsync(request);

        Assert.Equal(AdministrativeProvisioningStatus.PreviouslyApplied, result.Status);
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.OccurredAt);
    }

    [Fact]
    public async Task Concurrency_WhenTwoSimultaneousProvisionsForSameTargetUserWithDifferentEventIds_OneShouldSucceedAndOneShouldFailPrecondition()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId1 = Guid.NewGuid();
        var eventId2 = Guid.NewGuid();

        var req1 = new AdministrativeProvisioningRequest(eventId1, targetUserId, AdministrativeProvisioningAction.Provision, "op_c1", "TICKET-C1");
        var req2 = new AdministrativeProvisioningRequest(eventId2, targetUserId, AdministrativeProvisioningAction.Provision, "op_c2", "TICKET-C2");

        var task1 = Task.Run(() => _executor.ExecuteAsync(req1));
        var task2 = Task.Run(() => _executor.ExecuteAsync(req2));

        var results = await Task.WhenAll(task1, task2);

        var successCount = results.Count(r => r.Status == AdministrativeProvisioningStatus.Success);
        var preconditionFailedCount = results.Count(r => r.Status == AdministrativeProvisioningStatus.PreconditionFailed);

        Assert.Equal(1, successCount);
        Assert.Equal(1, preconditionFailedCount);

        // En BD: solo 1 fila en administrator y solo el evento del ganador
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var adminCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = targetUserId });
        Assert.Equal(1, adminCount);

        var winner = results.Single(r => r.Status == AdministrativeProvisioningStatus.Success);
        var loser = results.Single(r => r.Status == AdministrativeProvisioningStatus.PreconditionFailed);

        var winnerExists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM yusay.administrative_provisioning_event WHERE event_id = @EventId)",
            new { EventId = winner.EventId });
        Assert.True(winnerExists);

        var loserExists = await conn.ExecuteScalarAsync<bool>(
            "SELECT EXISTS(SELECT 1 FROM yusay.administrative_provisioning_event WHERE event_id = @EventId)",
            new { EventId = loser.EventId });
        Assert.False(loserExists);
    }

    [Fact]
    public async Task Concurrency_WhenTwoSimultaneousExecutionsWithSameEventIdAndPayload_BothShouldCompleteSuccessfully()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        var req1 = new AdministrativeProvisioningRequest(eventId, targetUserId, AdministrativeProvisioningAction.Provision, "op_same", "TICKET-SAME");
        var req2 = new AdministrativeProvisioningRequest(eventId, targetUserId, AdministrativeProvisioningAction.Provision, "op_same", "TICKET-SAME");

        var task1 = Task.Run(() => _executor.ExecuteAsync(req1));
        var task2 = Task.Run(() => _executor.ExecuteAsync(req2));

        var results = await Task.WhenAll(task1, task2);

        Assert.All(results, r => Assert.True(r.IsSuccess));

        var statuses = results.Select(r => r.Status).ToList();
        Assert.Contains(AdministrativeProvisioningStatus.Success, statuses);
        Assert.Contains(AdministrativeProvisioningStatus.PreviouslyApplied, statuses);

        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var eventCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public async Task CommitIndeterminate_WhenEventAbortedAndAbsentInDatabase_ShouldReturnVerificationPendingWithoutRepeatingDml()
    {
        var targetUserId = await CreateIsolatedUserAccountAsync();
        var eventId = Guid.NewGuid();

        var localExecutor = new AdministrativeProvisioningExecutor(_connectionFactory)
        {
            OnBeforeCommitAsync = _ => throw new NpgsqlException(
                "Simulated socket disconnection during commit flush",
                new System.Net.Sockets.SocketException((int)System.Net.Sockets.SocketError.TimedOut))
        };

        var request = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_runner",
            "TICKET-INDETERMINATE-ABORT");

        var result = await localExecutor.ExecuteAsync(request);

        // Reconexión exitosa pero fila ausente -> VerificationPending sin reescritura ciega
        Assert.Equal(AdministrativeProvisioningStatus.VerificationPending, result.Status);
        Assert.False(result.IsSuccess);

        // Comprobación de que no se repitió DML a ciegas y la base quedó limpia
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var adminCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrator WHERE user_id = @UserId",
            new { UserId = targetUserId });
        Assert.Equal(0, adminCount);

        var eventCount = await conn.ExecuteScalarAsync<int>(
            "SELECT count(*) FROM yusay.administrative_provisioning_event WHERE event_id = @EventId",
            new { EventId = eventId });
        Assert.Equal(0, eventCount);
    }

    [Fact]
    public async Task Executor_UsesExclusivelyYusayAdminOpsRole_AndApiDoesNotReferenceAdminOperations()
    {
        // 1. Verificación de rol efectivo en la conexión del ejecutor
        await using var conn = await _connectionFactory.CreateOpenConnectionAsync();
        var currentUser = await conn.ExecuteScalarAsync<string>("SELECT current_user");
        Assert.Equal(OpsRole, currentUser);

        // 2. Verificación de aislamiento: Yusay.Api NO referencia a Yusay.AdminOperations
        var apiAssembly = typeof(Program).Assembly;
        var referencedAssemblies = apiAssembly.GetReferencedAssemblies();
        Assert.DoesNotContain(referencedAssemblies, a => a.Name == "Yusay.AdminOperations");
    }

    private async Task<Guid> CreateIsolatedUserAccountAsync()
    {
        var userId = Guid.NewGuid();
        var email = $"b8b3b_iso_{Guid.NewGuid():N}@yusay.local";

        await using var conn = await _fixture.ConnectionFactory.CreateOpenConnectionAsync();
        await conn.ExecuteAsync(
            @"INSERT INTO yusay.user_account (user_id, email, status, adult_confirmed_at)
              VALUES (@UserId, @Email, 'ACTIVE', clock_timestamp())",
            new { UserId = userId, Email = email });

        return userId;
    }
}
