using System.Data;
using Dapper;
using Npgsql;
using Yusay.AdminOperations.Contracts;

namespace Yusay.AdminOperations;

public sealed class AdministrativeProvisioningExecutor(IAdminOpsConnectionFactory connectionFactory) : IAdministrativeProvisioningExecutor
{
    private const int MaxTransientRetries = 3;
    private readonly IAdminOpsConnectionFactory _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));

    internal Func<NpgsqlConnection, Task>? OnBeforeCommitAsync { get; set; }

    public async Task<AdministrativeProvisioningResult> ExecuteAsync(
        AdministrativeProvisioningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        for (int attempt = 0; attempt <= MaxTransientRetries; attempt++)
        {
            if (attempt > 0)
            {
                var delayMs = (int)(50 * Math.Pow(2, attempt - 1)) + Random.Shared.Next(10, 50);
                await Task.Delay(delayMs, cancellationToken);
            }

            NpgsqlConnection? connection = null;
            try
            {
                connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);

                var preInspection = await QueryEventAsync(connection, request.EventId, null, cancellationToken);
                if (preInspection is not null)
                {
                    if (PayloadComparator.Matches(
                        preInspection.target_user_id,
                        preInspection.action,
                        preInspection.operator_identifier,
                        preInspection.ticket_ref,
                        request))
                    {
                        return new AdministrativeProvisioningResult(
                            AdministrativeProvisioningStatus.PreviouslyApplied,
                            request.EventId,
                            request.TargetUserId,
                            request.Action.ToDbValue(),
                            request.OperatorIdentifier,
                            request.TicketRef,
                            "Operación previamente aplicada con idéntico payload.",
                            preInspection.occurred_at);
                    }

                    return new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.CollisionConflict,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        $"El event_id '{request.EventId}' ya existe en la bitácora con un payload discrepante.",
                        preInspection.occurred_at);
                }

                var txResult = await ExecuteTransactionAsync(connection, request, attempt, cancellationToken);
                if (txResult.RetryRequired)
                {
                    continue;
                }

                return txResult.Result!;
            }
            catch (Exception ex) when (IsConnectionOrIndeterminateException(ex))
            {
                return await RevalidateWithFreshConnectionAsync(request, cancellationToken);
            }
            finally
            {
                if (connection is not null)
                {
                    try { await connection.DisposeAsync(); } catch { }
                }
            }
        }

        return new AdministrativeProvisioningResult(
            AdministrativeProvisioningStatus.Failed,
            request.EventId,
            request.TargetUserId,
            request.Action.ToDbValue(),
            request.OperatorIdentifier,
            request.TicketRef,
            $"Reintentos transitorios agotados ({MaxTransientRetries}) debido a concurrencia (40P01 / 40001).");
    }

    private async Task<(bool RetryRequired, AdministrativeProvisioningResult? Result)> ExecuteTransactionAsync(
        NpgsqlConnection connection,
        AdministrativeProvisioningRequest request,
        int attempt,
        CancellationToken cancellationToken)
    {
        await using var tx = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

        try
        {
            if (request.Action == AdministrativeProvisioningAction.Provision)
            {
                var adminRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO yusay.administrator (user_id) VALUES (@UserId)",
                        new { UserId = request.TargetUserId },
                        tx,
                        cancellationToken: cancellationToken));

                if (adminRows != 1)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.Failed,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        $"Se esperaba RowsAffected == 1 en administrator, pero se obtuvo {adminRows}."));
                }

                // 2. Registro en bitácora
                var eventRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        @"INSERT INTO yusay.administrative_provisioning_event
                          (event_id, target_user_id, action, operator_identifier, ticket_ref)
                          VALUES (@EventId, @UserId, @Action, @OperatorIdentifier, @TicketRef)",
                        new
                        {
                            EventId = request.EventId,
                            UserId = request.TargetUserId,
                            Action = request.Action.ToDbValue(),
                            OperatorIdentifier = request.OperatorIdentifier,
                            TicketRef = request.TicketRef
                        },
                        tx,
                        cancellationToken: cancellationToken));

                if (eventRows != 1)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.Failed,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        $"Se esperaba RowsAffected == 1 en administrative_provisioning_event, pero se obtuvo {eventRows}."));
                }
            }
            else
            {
                var deletedRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        "DELETE FROM yusay.administrator WHERE user_id = @UserId",
                        new { UserId = request.TargetUserId },
                        tx,
                        cancellationToken: cancellationToken));

                if (deletedRows == 0)
                {
                    await tx.RollbackAsync(cancellationToken);

                    var reval = await QueryEventAsync(connection, request.EventId, null, cancellationToken);
                    if (reval is not null)
                    {
                        if (PayloadComparator.Matches(reval.target_user_id, reval.action, reval.operator_identifier, reval.ticket_ref, request))
                        {
                            return (false, new AdministrativeProvisioningResult(
                                AdministrativeProvisioningStatus.PreviouslyApplied,
                                request.EventId,
                                request.TargetUserId,
                                request.Action.ToDbValue(),
                                request.OperatorIdentifier,
                                request.TicketRef,
                                "Operación de baja previamente aplicada con idéntico payload.",
                                reval.occurred_at));
                        }

                        return (false, new AdministrativeProvisioningResult(
                            AdministrativeProvisioningStatus.CollisionConflict,
                            request.EventId,
                            request.TargetUserId,
                            request.Action.ToDbValue(),
                            request.OperatorIdentifier,
                            request.TicketRef,
                            $"El event_id '{request.EventId}' ya existe en la bitácora con un payload discrepante.",
                            reval.occurred_at));
                    }

                    return (false, new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.PreconditionFailed,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        "La cuenta objetivo no poseía la habilitación administrativa en yusay.administrator."));
                }

                var eventRows = await connection.ExecuteAsync(
                    new CommandDefinition(
                        @"INSERT INTO yusay.administrative_provisioning_event
                          (event_id, target_user_id, action, operator_identifier, ticket_ref)
                          VALUES (@EventId, @UserId, @Action, @OperatorIdentifier, @TicketRef)",
                        new
                        {
                            EventId = request.EventId,
                            UserId = request.TargetUserId,
                            Action = request.Action.ToDbValue(),
                            OperatorIdentifier = request.OperatorIdentifier,
                            TicketRef = request.TicketRef
                        },
                        tx,
                        cancellationToken: cancellationToken));

                if (eventRows != 1)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return (false, new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.Failed,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        $"Se esperaba RowsAffected == 1 en administrative_provisioning_event, pero se obtuvo {eventRows}."));
                }
            }

            try
            {
                if (OnBeforeCommitAsync is not null)
                {
                    await OnBeforeCommitAsync(connection);
                }

                await tx.CommitAsync(cancellationToken);
            }
            catch (Exception commitEx) when (IsConnectionOrIndeterminateException(commitEx))
            {
                var recovered = await RevalidateWithFreshConnectionAsync(request, cancellationToken);
                return (false, recovered);
            }

            // Lectura del timestamp UTC asentado
            var committedEvent = await QueryEventAsync(connection, request.EventId, null, cancellationToken);
            return (false, new AdministrativeProvisioningResult(
                AdministrativeProvisioningStatus.Success,
                request.EventId,
                request.TargetUserId,
                request.Action.ToDbValue(),
                request.OperatorIdentifier,
                request.TicketRef,
                "Operación administrativa ejecutada y confirmada exitosamente.",
                committedEvent?.occurred_at));
        }
        catch (PostgresException pgEx)
        {
            try { await tx.RollbackAsync(cancellationToken); } catch { }

            if ((pgEx.SqlState is "40P01" or "40001") && attempt < MaxTransientRetries)
            {
                return (true, null);
            }

            if (pgEx.SqlState == "23505")
            {
                var reval = await QueryEventAsync(connection, request.EventId, null, cancellationToken);
                if (reval is not null)
                {
                    if (PayloadComparator.Matches(reval.target_user_id, reval.action, reval.operator_identifier, reval.ticket_ref, request))
                    {
                        return (false, new AdministrativeProvisioningResult(
                            AdministrativeProvisioningStatus.PreviouslyApplied,
                            request.EventId,
                            request.TargetUserId,
                            request.Action.ToDbValue(),
                            request.OperatorIdentifier,
                            request.TicketRef,
                            "Operación previamente aplicada con idéntico payload.",
                            reval.occurred_at));
                    }

                    return (false, new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.CollisionConflict,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        $"El event_id '{request.EventId}' ya existe en la bitácora con un payload discrepante.",
                        reval.occurred_at));
                }

                if (string.Equals(pgEx.ConstraintName, "pk_administrator", StringComparison.OrdinalIgnoreCase))
                {
                    return (false, new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.PreconditionFailed,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        "La cuenta objetivo ya posee la habilitación administrativa en yusay.administrator."));
                }

                return (false, new AdministrativeProvisioningResult(
                    AdministrativeProvisioningStatus.Failed,
                    request.EventId,
                    request.TargetUserId,
                    request.Action.ToDbValue(),
                    request.OperatorIdentifier,
                    request.TicketRef,
                    $"Conflicto de unicidad 23505 en {pgEx.ConstraintName}: {pgEx.Message}"));
            }

            return (false, new AdministrativeProvisioningResult(
                AdministrativeProvisioningStatus.Failed,
                request.EventId,
                request.TargetUserId,
                request.Action.ToDbValue(),
                request.OperatorIdentifier,
                request.TicketRef,
                $"Error relacional PostgreSQL [{pgEx.SqlState}]: {pgEx.Message}"));
        }
    }

    private async Task<AdministrativeProvisioningResult> RevalidateWithFreshConnectionAsync(
        AdministrativeProvisioningRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var freshConn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
            var record = await QueryEventAsync(freshConn, request.EventId, null, cancellationToken);

            if (record is not null)
            {
                if (PayloadComparator.Matches(record.target_user_id, record.action, record.operator_identifier, record.ticket_ref, request))
                {
                    return new AdministrativeProvisioningResult(
                        AdministrativeProvisioningStatus.PreviouslyApplied,
                        request.EventId,
                        request.TargetUserId,
                        request.Action.ToDbValue(),
                        request.OperatorIdentifier,
                        request.TicketRef,
                        "Operación confirmada exitosamente tras revalidación con nueva conexión.",
                        record.occurred_at);
                }

                return new AdministrativeProvisioningResult(
                    AdministrativeProvisioningStatus.CollisionConflict,
                    request.EventId,
                    request.TargetUserId,
                    request.Action.ToDbValue(),
                    request.OperatorIdentifier,
                    request.TicketRef,
                    $"El event_id '{request.EventId}' existe con payload discrepante tras revalidación.",
                    record.occurred_at);
            }

            return new AdministrativeProvisioningResult(
                AdministrativeProvisioningStatus.VerificationPending,
                request.EventId,
                request.TargetUserId,
                request.Action.ToDbValue(),
                request.OperatorIdentifier,
                request.TicketRef,
                "Se interrumpió la conexión durante COMMIT y la fila no fue encontrada en la revalidación. Estado PENDIENTE_VERIFICACION sin reescritura ciega.");
        }
        catch (Exception ex)
        {
            return new AdministrativeProvisioningResult(
                AdministrativeProvisioningStatus.VerificationPending,
                request.EventId,
                request.TargetUserId,
                request.Action.ToDbValue(),
                request.OperatorIdentifier,
                request.TicketRef,
                $"Fallo de conexión al revalidar tras COMMIT indeterminado ({ex.Message}). Estado PENDIENTE_VERIFICACION persistente sin reescritura ciega.");
        }
    }

    private static async Task<PersistedProvisioningRecord?> QueryEventAsync(
        NpgsqlConnection connection,
        Guid eventId,
        NpgsqlTransaction? transaction,
        CancellationToken cancellationToken)
    {
        const string sql = @"
            SELECT target_user_id, action, operator_identifier, ticket_ref, occurred_at
            FROM yusay.administrative_provisioning_event
            WHERE event_id = @EventId";

        return await connection.QuerySingleOrDefaultAsync<PersistedProvisioningRecord>(
            new CommandDefinition(sql, new { EventId = eventId }, transaction, cancellationToken: cancellationToken));
    }

    private static bool IsConnectionOrIndeterminateException(Exception ex) =>
        ex is NpgsqlException or IOException or TimeoutException or OperationCanceledException;

    private sealed class PersistedProvisioningRecord
    {
        public Guid target_user_id { get; set; }
        public string action { get; set; } = string.Empty;
        public string operator_identifier { get; set; } = string.Empty;
        public string? ticket_ref { get; set; }
        public DateTimeOffset occurred_at { get; set; }
    }
}
