using System.Text.RegularExpressions;

namespace Yusay.AdminCli.Parsing;

public sealed class CliParseException : Exception
{
    public int ExitCode { get; }
    public CliParseException(string message, int exitCode = 5) : base(message)
    {
        ExitCode = exitCode;
    }
}

public static class CliParser
{
    private static readonly HashSet<string> ForbiddenCredentialFlags = new(StringComparer.OrdinalIgnoreCase)
    {
        "--password", "-p", "--pwd", "--connection-string", "--conn", "--db-password", "--secret"
    };

    private static readonly Regex CredentialPattern = new(
        @"(?i)(password|pwd|user\s*id|uid)\s*=",
        RegexOptions.Compiled);

    public static CliCommand Parse(string[] args)
    {
        if (args is null || args.Length == 0)
        {
            return new CliCommand(CommandType.Help);
        }

        // 1. Detección estricta y rechazo de contraseñas o cadenas de conexión en argumentos sin exponer su contenido
        foreach (var arg in args)
        {
            var flag = arg.Split('=')[0].Trim();
            if (ForbiddenCredentialFlags.Contains(flag) || CredentialPattern.IsMatch(arg))
            {
                throw new CliParseException(
                    "Violación de seguridad: Las credenciales y cadenas de conexión de base de datos no deben suministrarse mediante argumentos CLI. " +
                    "Configure la variable de entorno YUSAY_ADMIN_OPS_CONNECTION_STRING.",
                    exitCode: 5);
            }
        }

        var commandName = args[0].ToLowerInvariant();
        if (commandName is "help" or "--help" or "-h")
        {
            return new CliCommand(CommandType.Help);
        }

        var commandType = commandName switch
        {
            "provision" => CommandType.Provision,
            "revoke" => CommandType.Revoke,
            "verify" => CommandType.Verify,
            "list-pending" => CommandType.ListPending,
            _ => throw new CliParseException("Comando desconocido. Ejecute 'help' para ver los comandos válidos.")
        };

        Guid? targetUserId = null;
        string? operatorIdentifier = null;
        string? ticketRef = null;
        Guid? eventId = null;
        string? stateDir = null;

        for (int i = 1; i < args.Length; i++)
        {
            var current = args[i];

            if (current.Equals("--user", StringComparison.OrdinalIgnoreCase))
            {
                targetUserId = ParseGuidOption(args, ref i, "--user");
            }
            else if (current.Equals("--operator", StringComparison.OrdinalIgnoreCase))
            {
                operatorIdentifier = ParseStringOption(args, ref i, "--operator");
            }
            else if (current.Equals("--ticket", StringComparison.OrdinalIgnoreCase))
            {
                ticketRef = ParseStringOption(args, ref i, "--ticket");
            }
            else if (current.Equals("--event-id", StringComparison.OrdinalIgnoreCase))
            {
                eventId = ParseGuidOption(args, ref i, "--event-id");
            }
            else if (current.Equals("--state-dir", StringComparison.OrdinalIgnoreCase))
            {
                stateDir = ParseStringOption(args, ref i, "--state-dir");
            }
            else
            {
                // No se interpola el valor de la opción desconocida para evitar reflejar secretos en logs o errores
                throw new CliParseException("Opción desconocida o mal formateada. Verifique la sintaxis ejecutando 'help'.");
            }
        }

        // Validación de opciones requeridas por comando
        switch (commandType)
        {
            case CommandType.Provision:
            case CommandType.Revoke:
                if (!targetUserId.HasValue || targetUserId.Value == Guid.Empty)
                    throw new CliParseException("La opción '--user <uuid>' es obligatoria y debe ser un UUID válido.");

                if (string.IsNullOrWhiteSpace(operatorIdentifier))
                    throw new CliParseException("La opción '--operator <identificador>' es obligatoria para registrar la atribución en auditoría.");
                break;

            case CommandType.Verify:
                if (!eventId.HasValue || eventId.Value == Guid.Empty)
                    throw new CliParseException("La opción '--event-id <uuid>' es obligatoria para el comando verify.");
                break;
        }

        return new CliCommand(commandType, targetUserId, operatorIdentifier, ticketRef, eventId, stateDir);
    }

    private static Guid ParseGuidOption(string[] args, ref int index, string optionName)
    {
        index++;
        if (index >= args.Length || !Guid.TryParse(args[index], out var parsed) || parsed == Guid.Empty)
        {
            throw new CliParseException($"La opción '{optionName}' requiere un UUID v4 válido.");
        }
        return parsed;
    }

    private static string ParseStringOption(string[] args, ref int index, string optionName)
    {
        index++;
        if (index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new CliParseException($"La opción '{optionName}' requiere un valor de texto no vacío.");
        }

        var val = args[index].Trim();
        if (CredentialPattern.IsMatch(val))
        {
            throw new CliParseException(
                "Violación de seguridad: El valor suministrado contiene patrones de credenciales no autorizados.",
                exitCode: 5);
        }

        return val;
    }
}
