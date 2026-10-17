using System.Text.Json;

namespace Yusay.AdminCli.Storage;

/// <summary>
/// Diario local de operaciones administrativas fuera de banda (MP-PHYS-017).
///
/// Garantías y limitaciones técnicas reales por sistema operativo:
/// 1. Pre-persistencia y vaciado a disco:
///    - Todo registro se serializa primero en un archivo temporal (.tmp-*) único.
///    - Se invoca FileStream.Flush(flushToDisk: true):
///      * En Windows: Ejecuta Win32 FlushFileBuffers(handle), ordenando al sistema operativo y
///        controlador transferir las páginas en memoria caché al almacenamiento físico.
///      * En Linux/POSIX: Ejecuta la llamada al sistema fsync(fd).
///    - Limitación de hardware: En discos o entornos de almacenamiento sin respaldo por batería
///      (BBWC) ni protección ante cortes de energía (Power Loss Protection - PLP), memorias caché
///      volátiles internas del disco físico podrían perder datos ante un corte eléctrico instantáneo.
///      Por tanto, se garantiza durabilidad frente a caídas de proceso (crash/kill/reboot limpio),
///      pero la resistencia absoluta ante corte abrupto de alimentación depende del hardware y
///      políticas de barrera de escritura del controlador.
/// 2. Publicación atómica y protección contra sobreescritura:
///    - El archivo temporal se traslada a su ruta definitiva (operation-{eventId}.json) mediante File.Move.
///    - En Windows: Ejecuta MoveFileExW de forma atómica dentro del mismo volumen NTFS/ReFS.
///    - En Linux/POSIX: Ejecuta la llamada al sistema rename(2), garantizando visibilidad atómica en el directorio.
///    - Limitación de durabilidad de directorio en POSIX: rename(2) actualiza atómicamente la entrada
///      de directorio en memoria del kernel, pero la persistencia física del propio inodo de directorio
///      ante corte abrupto requeriría fsync sobre el descriptor de directorio (no expuesto directamente por .NET).
///    - En PersistPendingAsync nunca se permite sobrescribir un event_id existente (overwrite: false).
/// 3. Bloqueo exclusivo y concurrencia:
///    - Un FileStream exclusivo con FileShare.None sobre '.journal.lock' impide ejecuciones paralelas de
///      múltiples procesos CLI sobre el mismo directorio.
///    - A nivel intra-proceso, un SemaphoreSlim sincroniza accesos concurrentes.
/// 4. Reconciliación de temporales huérfanos sin pérdida de información:
///    - Al iniciar, se inspeccionan archivos temporales '.tmp-*.json'. Si un archivo temporal contiene
///      un registro íntegro cuya publicación final fue interrumpida por caída previa del proceso,
///      se recupera hacia su ruta definitiva. Si está truncado o corrupto, se traslada a cuarentena
///      ('.quarantine-*') para preservar evidencia forense sin destruir datos ni interrumpir el servicio.
/// 5. Permisos de archivo y directorio:
///    - En POSIX (Linux/macOS): El directorio se protege con 0700 (solo usuario) y los archivos con 0600.
///    - En Windows: Se basa en las ACLs del perfil del usuario en NTFS, restringiendo el acceso al
///      usuario ejecutor y administradores del sistema.
/// </summary>
public sealed class FileOperationJournal : IOperationJournal
{
    private const string LockFileName = ".journal.lock";
    private readonly string _stateDirectory;
    private readonly FileStream _lockStream;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public FileOperationJournal(string stateDirectory)
    {
        if (string.IsNullOrWhiteSpace(stateDirectory))
            throw new ArgumentException("State directory must not be empty.", nameof(stateDirectory));

        _stateDirectory = Path.GetFullPath(stateDirectory);
        Directory.CreateDirectory(_stateDirectory);
        ApplyDirectoryPermissions(_stateDirectory);

        // Bloqueo exclusivo del diario local a nivel de sistema operativo
        var lockFilePath = Path.Combine(_stateDirectory, LockFileName);
        try
        {
            _lockStream = new FileStream(
                lockFilePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);

            ApplyFilePermissions(lockFilePath);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                $"No se pudo adquirir el bloqueo exclusivo sobre el diario local '{lockFilePath}'. " +
                "Existe otra instancia de Yusay.AdminCli en ejecución sobre este directorio de estado.",
                ex);
        }

        // Reconciliación segura de temporales huérfanos sin pérdida de información
        ReconcileOrphanedTempFiles();
    }

    public async Task PersistPendingAsync(LocalOperationRecord record, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var finalPath = GetRecordPath(record.EventId);
            if (File.Exists(finalPath))
            {
                throw new InvalidOperationException(
                    $"El event_id '{record.EventId}' ya existe en el diario local. " +
                    "Un event_id persistido no puede ser sobrescrito con una nueva operación.");
            }

            await EnsureNoConflictingPendingOperationInternalAsync(record.TargetUserId, cancellationToken);

            var tempPath = Path.Combine(_stateDirectory, $".tmp-{record.EventId:N}-{Guid.NewGuid():N}.json");

            try
            {
                await WriteRecordAtomicallyAsync(tempPath, finalPath, record, overwrite: false, cancellationToken);
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
                throw new InvalidOperationException(
                    $"Fallo de persistencia durable en el diario local para event_id '{record.EventId}'. " +
                    "La operación abortó de forma segura antes de iniciar cualquier llamada relacional.",
                    ex);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateStatusAsync(
        Guid eventId,
        LocalOperationStatus newStatus,
        string? message,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var existing = await GetByEventIdInternalAsync(eventId, cancellationToken)
                ?? throw new KeyNotFoundException($"No se encontró el registro para event_id '{eventId}' en el diario local.");

            // Preservación estricta de campos inmutables (TargetUserId, Action, OperatorIdentifier, TicketRef, CreatedAtUtc)
            var updated = existing with
            {
                Status = newStatus,
                LastUpdatedAtUtc = DateTimeOffset.UtcNow,
                LastMessage = message
            };

            var finalPath = GetRecordPath(eventId);
            var tempPath = Path.Combine(_stateDirectory, $".tmp-{eventId:N}-{Guid.NewGuid():N}.json");

            await WriteRecordAtomicallyAsync(tempPath, finalPath, updated, overwrite: true, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<LocalOperationRecord?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await GetByEventIdInternalAsync(eventId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<IReadOnlyList<LocalOperationRecord>> ListPendingAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = await LoadAllRecordsInternalAsync(cancellationToken);
            return records.Where(r => !r.Status.IsTerminal()).OrderBy(r => r.CreatedAtUtc).ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task EnsureNoConflictingPendingOperationAsync(Guid targetUserId, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureNoConflictingPendingOperationInternalAsync(targetUserId, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _lockStream.Dispose();
        _gate.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _lockStream.DisposeAsync();
        _gate.Dispose();
    }

    private string GetRecordPath(Guid eventId) =>
        Path.Combine(_stateDirectory, $"operation-{eventId:N}.json");

    private async Task<LocalOperationRecord?> GetByEventIdInternalAsync(Guid eventId, CancellationToken cancellationToken)
    {
        var path = GetRecordPath(eventId);
        if (!File.Exists(path))
            return null;

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return await JsonSerializer.DeserializeAsync<LocalOperationRecord>(stream, JsonOptions, cancellationToken);
    }

    private async Task EnsureNoConflictingPendingOperationInternalAsync(Guid targetUserId, CancellationToken cancellationToken)
    {
        var allRecords = await LoadAllRecordsInternalAsync(cancellationToken);
        var conflicting = allRecords.FirstOrDefault(r => r.TargetUserId == targetUserId && !r.Status.IsTerminal());

        if (conflicting is not null)
        {
            throw new InvalidOperationException(
                $"Existe una operación no terminal activa para el usuario objetivo '{targetUserId}' con event_id '{conflicting.EventId}' " +
                $"(Estado: {conflicting.Status}). Ejecute 'verify --event-id {conflicting.EventId}' para reconciliarla " +
                "antes de iniciar una nueva acción sobre este usuario.");
        }
    }

    private async Task<List<LocalOperationRecord>> LoadAllRecordsInternalAsync(CancellationToken cancellationToken)
    {
        var result = new List<LocalOperationRecord>();
        var files = Directory.GetFiles(_stateDirectory, "operation-*.json");

        foreach (var file in files)
        {
            try
            {
                await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                var record = await JsonSerializer.DeserializeAsync<LocalOperationRecord>(stream, JsonOptions, cancellationToken);
                if (record is not null)
                {
                    result.Add(record);
                }
            }
            catch (JsonException)
            {
                // Archivo ilegible sin interrumpir la lista
            }
        }

        return result;
    }

    private void ReconcileOrphanedTempFiles()
    {
        var tempFiles = Directory.GetFiles(_stateDirectory, ".tmp-*.json");
        foreach (var tempFile in tempFiles)
        {
            try
            {
                var content = File.ReadAllText(tempFile);
                var record = JsonSerializer.Deserialize<LocalOperationRecord>(content, JsonOptions);
                if (record is not null && record.EventId != Guid.Empty)
                {
                    var finalPath = GetRecordPath(record.EventId);
                    if (!File.Exists(finalPath))
                    {
                        // Se recupera la publicación pendiente interrumpida sin pérdida de información
                        File.Move(tempFile, finalPath, overwrite: false);
                        ApplyFilePermissions(finalPath);
                        continue;
                    }
                }

                // Si finalPath ya existe de forma íntegra, el temporal redundante se elimina
                File.Delete(tempFile);
            }
            catch
            {
                // Si el archivo temporal está corrupto o truncado por la caída de proceso,
                // se preserva en cuarentena para no destruir evidencia forense.
                var quarantinePath = Path.Combine(_stateDirectory, $".quarantine-{Path.GetFileName(tempFile)}");
                try
                {
                    if (!File.Exists(quarantinePath))
                    {
                        File.Move(tempFile, quarantinePath);
                        ApplyFilePermissions(quarantinePath);
                    }
                }
                catch { }
            }
        }
    }

    private static async Task WriteRecordAtomicallyAsync(
        string tempPath,
        string finalPath,
        LocalOperationRecord record,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        await using (var stream = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 4096,
            FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, record, JsonOptions, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            stream.Flush(flushToDisk: true); // Vacía los búferes del sistema de archivos al controlador
        }

        ApplyFilePermissions(tempPath);

        File.Move(tempPath, finalPath, overwrite);

        ApplyFilePermissions(finalPath);
    }

    private static void ApplyDirectoryPermissions(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch { }
        }
    }

    private static void ApplyFilePermissions(string path)
    {
        if (!OperatingSystem.IsWindows() && File.Exists(path))
        {
            try
            {
                File.SetUnixFileMode(path,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite);
            }
            catch { }
        }
    }
}
