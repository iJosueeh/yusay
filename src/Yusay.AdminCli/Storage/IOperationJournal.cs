namespace Yusay.AdminCli.Storage;

public interface IOperationJournal : IDisposable, IAsyncDisposable
{
    Task PersistPendingAsync(LocalOperationRecord record, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(Guid eventId, LocalOperationStatus newStatus, string? message, CancellationToken cancellationToken = default);
    Task<LocalOperationRecord?> GetByEventIdAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocalOperationRecord>> ListPendingAsync(CancellationToken cancellationToken = default);
    Task EnsureNoConflictingPendingOperationAsync(Guid targetUserId, CancellationToken cancellationToken = default);
}
