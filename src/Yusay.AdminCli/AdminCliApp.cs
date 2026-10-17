using Dapper;
using Npgsql;
using Yusay.AdminCli.Parsing;
using Yusay.AdminCli.Storage;
using Yusay.AdminOperations;
using Yusay.AdminOperations.Contracts;

namespace Yusay.AdminCli;

public sealed class AdminCliApp
{
    private const string EnvConnectionString = "YUSAY_ADMIN_OPS_CONNECTION_STRING";
    private const string AltEnvConnectionString = "DATABASE_ADMIN_URL";

    private readonly TextWriter _stdout;
    private readonly TextWriter _stderr;
    private readonly Func<string, string?> _envProvider;
    private readonly Func<string, IOperationJournal> _journalFactory;
    private readonly Func<string, IAdminOpsConnectionFactory> _connectionFactoryBuilder;

    public AdminCliApp(
        TextWriter? stdout = null,
        TextWriter? stderr = null,
        Func<string, string?>? envProvider = null,
        Func<string, IOperationJournal>? journalFactory = null,
        Func<string, IAdminOpsConnectionFactory>? connectionFactoryBuilder = null)
    {
        _stdout = stdout ?? Console.Out;
        _stderr = stderr ?? Console.Error;
        _envProvider = envProvider ?? Environment.GetEnvironmentVariable;
        _journalFactory = journalFactory ?? (dir => new FileOperationJournal(dir));
        _connectionFactoryBuilder = connectionFactoryBuilder ?? (cs => new AdminOpsConnectionFactory(cs));
    }

    public async Task<int> RunAsync(string[] args, CancellationToken cancellationToken = default)
    {
        CliCommand command;
        try
        {
            command = CliParser.Parse(args);
        }
        catch (CliParseException ex)
        {
            await _stderr.WriteLineAsync($"[ERROR] {ex.Message}");
            return ex.ExitCode;
        }

        if (command.Type == CommandType.Help)
        {
            await PrintHelpAsync();
            return 0;
        }

        var stateDir = ResolveStateDirectory(command.StateDirectory);

        try
        {
            await using var journal = _journalFactory(stateDir);

            if (command.Type == CommandType.ListPending)
            {
                return await HandleListPendingAsync(journal, cancellationToken);
            }

            var connectionString = ResolveConnectionString();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                await _stderr.WriteLineAsync(
                    $"[ERROR] No se configuró la cadena de conexión de yusay_admin_ops. " +
                    $"Defina la variable de entorno '{EnvConnectionString}' (o '{AltEnvConnectionString}').");
                return 5;
            }

            var connectionFactory = _connectionFactoryBuilder(connectionString);

            return command.Type switch
            {
                CommandType.Provision => await HandleProvisionOrRevokeAsync(
                    command, AdministrativeProvisioningAction.Provision, journal, connectionFactory, cancellationToken),
                CommandType.Revoke => await HandleProvisionOrRevokeAsync(
                    command, AdministrativeProvisioningAction.Revoke, journal, connectionFactory, cancellationToken),
                CommandType.Verify => await HandleVerifyAsync(
                    command, journal, connectionFactory, cancellationToken),
                _ => 5
            };
        }
        catch (Exception ex)
        {
            await _stderr.WriteLineAsync($"[ERROR FATAL] {SanitizeSensitiveText(ex.Message)}");
            return 5;
        }
    }

    private async Task<int> HandleProvisionOrRevokeAsync(
        CliCommand command,
        AdministrativeProvisioningAction action,
        IOperationJournal journal,
        IAdminOpsConnectionFactory connectionFactory,
        CancellationToken cancellationToken)
    {
        var targetUserId = command.TargetUserId!.Value;
        var operatorIdentifier = command.OperatorIdentifier!;
        var ticketRef = command.TicketRef;
        var eventId = command.EventId ?? Guid.NewGuid();

        // 1. Validar que no existan operaciones previas no terminales para el mismo usuario
        await journal.EnsureNoConflictingPendingOperationAsync(targetUserId, cancellationToken);

        // 2. Persistir durablemente el registro local antes de cualquier llamada a base de datos (fsync obligatorio)
        var localRecord = new LocalOperationRecord
        {
            EventId = eventId,
            TargetUserId = targetUserId,
            Action = action == AdministrativeProvisioningAction.Provision ? "Provision" : "Revoke",
            OperatorIdentifier = operatorIdentifier,
            TicketRef = ticketRef,
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await journal.PersistPendingAsync(localRecord, cancellationToken);

        // 3. Ejecución relacional atómica
        var request = new AdministrativeProvisioningRequest(
            eventId,
            targetUserId,
            action,
            operatorIdentifier,
            ticketRef);

        var executor = new AdministrativeProvisioningExecutor(connectionFactory);
        var result = await executor.ExecuteAsync(request, cancellationToken);

        // 4. Mapeo y actualización de diario local
        var (localStatus, exitCode) = MapResultStatus(result.Status);
        await journal.UpdateStatusAsync(eventId, localStatus, result.Message, cancellationToken);

        // 5. Salida estructurada
        await _stdout.WriteLineAsync(
            $"[RESULT] EventId: {result.EventId} | Status: {result.Status} | Action: {result.Action} " +
            $"| TargetUser: {result.TargetUserId} | Operator: {result.OperatorIdentifier} " +
            $"| OccurredAt: {result.OccurredAt:O} | Message: {result.Message}");

        return exitCode;
    }

    private async Task<int> HandleVerifyAsync(
        CliCommand command,
        IOperationJournal journal,
        IAdminOpsConnectionFactory connectionFactory,
        CancellationToken cancellationToken)
    {
        var eventId = command.EventId!.Value;
        var localRecord = await journal.GetByEventIdAsync(eventId, cancellationToken);

        // Consulta de solo lectura a PostgreSQL
        await using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        const string querySql = @"
            SELECT target_user_id, action, operator_identifier, ticket_ref, occurred_at
            FROM yusay.administrative_provisioning_event
            WHERE event_id = @EventId";

        var dbEvent = await connection.QuerySingleOrDefaultAsync<PersistedEventRow>(
            new CommandDefinition(querySql, new { EventId = eventId }, cancellationToken: cancellationToken));

        if (dbEvent is not null)
        {
            // Evento confirmado en PostgreSQL
            if (localRecord is not null)
            {
                var actionEnum = string.Equals(localRecord.Action, "Provision", StringComparison.OrdinalIgnoreCase)
                    ? AdministrativeProvisioningAction.Provision
                    : AdministrativeProvisioningAction.Revoke;

                var req = new AdministrativeProvisioningRequest(
                    localRecord.EventId,
                    localRecord.TargetUserId,
                    actionEnum,
                    localRecord.OperatorIdentifier,
                    localRecord.TicketRef);

                var matches = PayloadComparator.Matches(
                    dbEvent.target_user_id,
                    dbEvent.action,
                    dbEvent.operator_identifier,
                    dbEvent.ticket_ref,
                    req);

                if (matches)
                {
                    await journal.UpdateStatusAsync(
                        eventId,
                        LocalOperationStatus.CompletedPreviouslyApplied,
                        "Verificación confirmada exitosamente con payload coincidente.",
                        cancellationToken);

                    await _stdout.WriteLineAsync(
                        $"[VERIFIED SUCCESS] EventId: {eventId} | TargetUser: {dbEvent.target_user_id} " +
                        $"| Action: {dbEvent.action} | Operator: {dbEvent.operator_identifier} " +
                        $"| OccurredAt: {dbEvent.occurred_at:O} (Coincidencia plena confirmada).");

                    return 0; // ExitCode 0
                }

                await journal.UpdateStatusAsync(
                    eventId,
                    LocalOperationStatus.CompletedCollisionConflict,
                    "Conflicto de colisión: el evento en base de datos difiere del diario local.",
                    cancellationToken);

                await _stderr.WriteLineAsync(
                    $"[VERIFIED CONFLICT] EventId: {eventId} existe en PostgreSQL pero su contenido difiere del diario local.");

                return 3; // ExitCode 3: CollisionConflict
            }

            await _stdout.WriteLineAsync(
                $"[VERIFIED SUCCESS] EventId: {eventId} | TargetUser: {dbEvent.target_user_id} " +
                $"| Action: {dbEvent.action} | Operator: {dbEvent.operator_identifier} | OccurredAt: {dbEvent.occurred_at:O}");

            return 0;
        }

        // Evento no encontrado en PostgreSQL: de solo lectura, conserva VerificationPending sin DML
        if (localRecord is not null)
        {
            await journal.UpdateStatusAsync(
                eventId,
                LocalOperationStatus.VerificationPending,
                "Verificación de solo lectura: el evento no se encuentra en PostgreSQL. Se conserva VerificationPending sin ejecutar DML.",
                cancellationToken);
        }

        await _stderr.WriteLineAsync(
            $"[VERIFIED PENDING] El evento con event_id '{eventId}' NO fue encontrado en PostgreSQL. " +
            "La operación permanece en estado VerificationPending (PENDIENTE_VERIFICACION). No se ejecutó ningún DML.");

        return 2; // ExitCode 2: VerificationPending
    }

    private async Task<int> HandleListPendingAsync(IOperationJournal journal, CancellationToken cancellationToken)
    {
        var pendingList = await journal.ListPendingAsync(cancellationToken);

        if (pendingList.Count == 0)
        {
            await _stdout.WriteLineAsync("No existen operaciones administrativas pendientes en el diario local.");
            return 0;
        }

        await _stdout.WriteLineAsync($"Operaciones administrativas no terminales encontradas ({pendingList.Count}):");
        foreach (var p in pendingList)
        {
            await _stdout.WriteLineAsync(
                $"  EventId: {p.EventId} | Status: {p.Status} | Action: {p.Action} | TargetUser: {p.TargetUserId} " +
                $"| Operator: {p.OperatorIdentifier} | Ticket: {p.TicketRef ?? "(none)"} | Created: {p.CreatedAtUtc:u}");
        }

        return 0;
    }

    private static (LocalOperationStatus Status, int ExitCode) MapResultStatus(AdministrativeProvisioningStatus status) =>
        status switch
        {
            AdministrativeProvisioningStatus.Success => (LocalOperationStatus.CompletedSuccess, 0),
            AdministrativeProvisioningStatus.PreviouslyApplied => (LocalOperationStatus.CompletedPreviouslyApplied, 0),
            AdministrativeProvisioningStatus.PreconditionFailed => (LocalOperationStatus.CompletedPreconditionFailed, 1),
            AdministrativeProvisioningStatus.VerificationPending => (LocalOperationStatus.VerificationPending, 2),
            AdministrativeProvisioningStatus.CollisionConflict => (LocalOperationStatus.CompletedCollisionConflict, 3),
            AdministrativeProvisioningStatus.Failed => (LocalOperationStatus.CompletedFailed, 4),
            _ => (LocalOperationStatus.CompletedFailed, 5)
        };

    private string? ResolveConnectionString()
    {
        return _envProvider(EnvConnectionString) ?? _envProvider(AltEnvConnectionString);
    }

    private static string ResolveStateDirectory(string? customPath)
    {
        if (!string.IsNullOrWhiteSpace(customPath))
        {
            return Path.GetFullPath(customPath);
        }

        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(homeDir, ".yusay", "admin-ops");
    }

    private async Task PrintHelpAsync()
    {
        await _stdout.WriteLineAsync(@"Yusay Administrative Provisioning CLI (B8b-3C)
Uso: yusay-admin <comando> [opciones]

Comandos:
  provision       Habilita a un usuario como administrador (ALTA atómica y bitácora)
  revoke          Revoca la habilitación administrativa (BAJA atómica y bitácora)
  verify          Consulta de solo lectura en PostgreSQL para resolver estados pendientes
  list-pending    Lista operaciones no terminales en el diario local
  help            Muestra esta ayuda

Opciones:
  --user <uuid>         Identificador de la cuenta objetivo (obligatorio en provision/revoke)
  --operator <id>       Identificador del operador/servicio responsable (obligatorio en provision/revoke)
  --ticket <ref>        Referencia opcional a ticket de cambio o justificación
  --event-id <uuid>     Identificador UUID de idempotencia (obligatorio en verify; generado si se omite en provision/revoke)
  --state-dir <ruta>    Ruta personalizada del directorio del diario local (por defecto: ~/.yusay/admin-ops)

Seguridad:
  Las credenciales de base de datos se configuran mediante la variable de entorno YUSAY_ADMIN_OPS_CONNECTION_STRING.
  No se aceptan contraseñas ni cadenas de conexión mediante parámetros de línea de comandos.");
    }

    private sealed class PersistedEventRow
    {
        public Guid target_user_id { get; set; }
        public string action { get; set; } = string.Empty;
        public string operator_identifier { get; set; } = string.Empty;
        public string? ticket_ref { get; set; }
        public DateTimeOffset occurred_at { get; set; }
    }

    private static string SanitizeSensitiveText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        return System.Text.RegularExpressions.Regex.Replace(
            text,
            @"(?i)(password|pwd|secret)\s*=\s*[^;\s]+",
            "$1=***");
    }
}
