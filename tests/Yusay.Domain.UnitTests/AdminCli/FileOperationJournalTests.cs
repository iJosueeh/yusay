using Yusay.AdminCli.Storage;

namespace Yusay.Domain.UnitTests.AdminCli;

public sealed class FileOperationJournalTests : IDisposable
{
    private readonly string _tempDir;

    public FileOperationJournalTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"yusay_journal_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch { }
    }

    [Fact]
    public async Task PersistPendingAsync_ShouldWriteValidRecordToDisk()
    {
        using var journal = new FileOperationJournal(_tempDir);
        var eventId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();

        var record = new LocalOperationRecord
        {
            EventId = eventId,
            TargetUserId = targetUserId,
            Action = "Provision",
            OperatorIdentifier = "ops_agent",
            TicketRef = "TICKET-99",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await journal.PersistPendingAsync(record);

        var retrieved = await journal.GetByEventIdAsync(eventId);
        Assert.NotNull(retrieved);
        Assert.Equal(eventId, retrieved.EventId);
        Assert.Equal(targetUserId, retrieved.TargetUserId);
        Assert.Equal("Provision", retrieved.Action);
        Assert.Equal("ops_agent", retrieved.OperatorIdentifier);
        Assert.Equal("TICKET-99", retrieved.TicketRef);
        Assert.Equal(LocalOperationStatus.PendingExecution, retrieved.Status);
    }

    [Fact]
    public async Task PersistPendingAsync_WhenActiveNonTerminalOperationExistsForSameUser_ShouldThrowInvalidOperationException()
    {
        using var journal = new FileOperationJournal(_tempDir);
        var targetUserId = Guid.NewGuid();

        var firstRecord = new LocalOperationRecord
        {
            EventId = Guid.NewGuid(),
            TargetUserId = targetUserId,
            Action = "Provision",
            OperatorIdentifier = "ops1",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await journal.PersistPendingAsync(firstRecord);

        // Intento de segunda operación para el mismo usuario mientras la primera sigue no terminal
        var secondRecord = new LocalOperationRecord
        {
            EventId = Guid.NewGuid(),
            TargetUserId = targetUserId,
            Action = "Provision",
            OperatorIdentifier = "ops2",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            journal.PersistPendingAsync(secondRecord));

        Assert.Contains("Existe una operación no terminal activa", ex.Message);
    }

    [Fact]
    public async Task UpdateStatusAsync_WhenFirstOperationIsTerminal_AllowsNewOperationForSameUser()
    {
        using var journal = new FileOperationJournal(_tempDir);
        var targetUserId = Guid.NewGuid();
        var firstEventId = Guid.NewGuid();

        var firstRecord = new LocalOperationRecord
        {
            EventId = firstEventId,
            TargetUserId = targetUserId,
            Action = "Provision",
            OperatorIdentifier = "ops1",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await journal.PersistPendingAsync(firstRecord);

        // Se actualiza la primera a estado terminal
        await journal.UpdateStatusAsync(firstEventId, LocalOperationStatus.CompletedSuccess, "Finalizado");

        // Ahora debe permitirse una segunda operación para el mismo usuario
        var secondEventId = Guid.NewGuid();
        var secondRecord = new LocalOperationRecord
        {
            EventId = secondEventId,
            TargetUserId = targetUserId,
            Action = "Revoke",
            OperatorIdentifier = "ops2",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await journal.PersistPendingAsync(secondRecord);

        var retrieved = await journal.GetByEventIdAsync(secondEventId);
        Assert.NotNull(retrieved);
        Assert.Equal(LocalOperationStatus.PendingExecution, retrieved.Status);
    }

    [Fact]
    public void ExclusiveLock_WhenSecondInstanceAttemptsSameStateDirectory_ShouldThrowInvalidOperationException()
    {
        using var journal1 = new FileOperationJournal(_tempDir);

        var ex = Assert.Throws<InvalidOperationException>(() => new FileOperationJournal(_tempDir));
        Assert.Contains("No se pudo adquirir el bloqueo exclusivo", ex.Message);
    }

    [Fact]
    public async Task ListPendingAsync_ShouldReturnOnlyNonTerminalOperations()
    {
        using var journal = new FileOperationJournal(_tempDir);

        var pendingId = Guid.NewGuid();
        await journal.PersistPendingAsync(new LocalOperationRecord
        {
            EventId = pendingId,
            TargetUserId = Guid.NewGuid(),
            Action = "Provision",
            OperatorIdentifier = "op1",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        });

        var terminalId = Guid.NewGuid();
        await journal.PersistPendingAsync(new LocalOperationRecord
        {
            EventId = terminalId,
            TargetUserId = Guid.NewGuid(),
            Action = "Revoke",
            OperatorIdentifier = "op2",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await journal.UpdateStatusAsync(terminalId, LocalOperationStatus.CompletedSuccess, "Hecho");

        var pending = await journal.ListPendingAsync();
        Assert.Single(pending);
        Assert.Equal(pendingId, pending[0].EventId);
    }

    [Fact]
    public async Task PersistPendingAsync_WhenEventIdAlreadyExists_ThrowsInvalidOperationExceptionAndPreventsOverwrite()
    {
        using var journal = new FileOperationJournal(_tempDir);
        var eventId = Guid.NewGuid();
        var targetUserId1 = Guid.NewGuid();
        var targetUserId2 = Guid.NewGuid();

        var originalRecord = new LocalOperationRecord
        {
            EventId = eventId,
            TargetUserId = targetUserId1,
            Action = "Provision",
            OperatorIdentifier = "original_ops",
            TicketRef = "ORIG-100",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        await journal.PersistPendingAsync(originalRecord);

        // Intento de sobreescribir el mismo event_id con otro payload
        var maliciousRecord = new LocalOperationRecord
        {
            EventId = eventId,
            TargetUserId = targetUserId2,
            Action = "Revoke",
            OperatorIdentifier = "attacker_ops",
            TicketRef = "MAL-666",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            journal.PersistPendingAsync(maliciousRecord));

        Assert.Contains("ya existe en el diario local", ex.Message);

        // El registro original debe permanecer completamente inalterado
        var stored = await journal.GetByEventIdAsync(eventId);
        Assert.NotNull(stored);
        Assert.Equal(targetUserId1, stored.TargetUserId);
        Assert.Equal("Provision", stored.Action);
        Assert.Equal("original_ops", stored.OperatorIdentifier);
        Assert.Equal("ORIG-100", stored.TicketRef);
    }

    [Fact]
    public async Task PersistPendingAsync_ConcurrentWritesWithSameEventId_OnlyOneSucceeds()
    {
        using var journal = new FileOperationJournal(_tempDir);
        var eventId = Guid.NewGuid();

        var record1 = new LocalOperationRecord
        {
            EventId = eventId,
            TargetUserId = Guid.NewGuid(),
            Action = "Provision",
            OperatorIdentifier = "worker1",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var record2 = new LocalOperationRecord
        {
            EventId = eventId,
            TargetUserId = Guid.NewGuid(),
            Action = "Revoke",
            OperatorIdentifier = "worker2",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        var task1 = Task.Run(async () =>
        {
            try { await journal.PersistPendingAsync(record1); return true; }
            catch { return false; }
        });

        var task2 = Task.Run(async () =>
        {
            try { await journal.PersistPendingAsync(record2); return true; }
            catch { return false; }
        });

        var results = await Task.WhenAll(task1, task2);

        // Exactamente una tarea debe tener éxito y la otra fallar
        Assert.True(results[0] ^ results[1], "Exactamente una escritura concurrente debe triunfar y la otra fallar.");

        var stored = await journal.GetByEventIdAsync(eventId);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task EnsureNoConflictingPendingOperation_WhenStatusIsVerificationPending_ThrowsInvalidOperationException()
    {
        using var journal = new FileOperationJournal(_tempDir);
        var targetUserId = Guid.NewGuid();

        await journal.PersistPendingAsync(new LocalOperationRecord
        {
            EventId = Guid.NewGuid(),
            TargetUserId = targetUserId,
            Action = "Provision",
            OperatorIdentifier = "ops",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        });

        // Actualizar a VerificationPending
        var files = Directory.GetFiles(_tempDir, "operation-*.json");
        var eventId = Guid.Parse(Path.GetFileNameWithoutExtension(files[0]).Replace("operation-", ""));
        await journal.UpdateStatusAsync(eventId, LocalOperationStatus.VerificationPending, "Indeterminado");

        // Debe seguir bloqueando cualquier nueva operación sobre el mismo targetUserId
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            journal.EnsureNoConflictingPendingOperationAsync(targetUserId));

        Assert.Contains("Existe una operación no terminal activa", ex.Message);
        Assert.Contains("VerificationPending", ex.Message);
    }

    [Fact]
    public async Task ReconcileOrphanedTempFiles_WhenCompleteTempFileExistsWithoutFinalPath_RecoversRecordWithoutDataLoss()
    {
        var eventId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var record = new LocalOperationRecord
        {
            EventId = eventId,
            TargetUserId = targetUserId,
            Action = "Provision",
            OperatorIdentifier = "recovered_ops",
            TicketRef = "RECOV-1",
            Status = LocalOperationStatus.PendingExecution,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            LastUpdatedAtUtc = DateTimeOffset.UtcNow
        };

        // Simular un crash posterior al flush del temporal pero previo al File.Move
        var tempFile = Path.Combine(_tempDir, $".tmp-{eventId:N}-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(tempFile, System.Text.Json.JsonSerializer.Serialize(record, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        // Al instanciar FileOperationJournal, debe reconciliar y recuperar el registro
        using (var journal = new FileOperationJournal(_tempDir))
        {
            var recovered = await journal.GetByEventIdAsync(eventId);
            Assert.NotNull(recovered);
            Assert.Equal(eventId, recovered.EventId);
            Assert.Equal(targetUserId, recovered.TargetUserId);
            Assert.Equal("recovered_ops", recovered.OperatorIdentifier);

            // El archivo temporal no debe existir y la ruta final debe existir
            Assert.False(File.Exists(tempFile));
            var finalPath = Path.Combine(_tempDir, $"operation-{eventId:N}.json");
            Assert.True(File.Exists(finalPath));
        }
    }

    [Fact]
    public void ReconcileOrphanedTempFiles_WhenCorruptTempFileExists_QuarantinesFileWithoutDataLoss()
    {
        // Simular un archivo temporal truncado o corrupto por caída súbita durante serialización
        var tempFile = Path.Combine(_tempDir, $".tmp-corrupt-{Guid.NewGuid():N}.json");
        File.WriteAllText(tempFile, "{ \"EventId\": \"truncated-bad-json");

        // Al instanciar FileOperationJournal, el archivo corrupto debe ponerse en cuarentena
        using (var journal = new FileOperationJournal(_tempDir))
        {
            Assert.False(File.Exists(tempFile));
            var quarantineFiles = Directory.GetFiles(_tempDir, ".quarantine-*");
            Assert.Single(quarantineFiles);
            Assert.Contains("truncated-bad-json", File.ReadAllText(quarantineFiles[0]));
        }
    }
}
