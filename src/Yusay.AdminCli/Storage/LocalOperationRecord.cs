namespace Yusay.AdminCli.Storage;

public enum LocalOperationStatus
{
    PendingExecution,
    VerificationPending,
    CompletedSuccess,
    CompletedPreviouslyApplied,
    CompletedPreconditionFailed,
    CompletedCollisionConflict,
    CompletedFailed
}

public static class LocalOperationStatusExtensions
{
    public static bool IsTerminal(this LocalOperationStatus status) =>
        status is LocalOperationStatus.CompletedSuccess
               or LocalOperationStatus.CompletedPreviouslyApplied
               or LocalOperationStatus.CompletedPreconditionFailed
               or LocalOperationStatus.CompletedCollisionConflict
               or LocalOperationStatus.CompletedFailed;
}

public sealed record LocalOperationRecord
{
    public Guid EventId { get; init; }
    public Guid TargetUserId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string OperatorIdentifier { get; init; } = string.Empty;
    public string? TicketRef { get; init; }
    public LocalOperationStatus Status { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset LastUpdatedAtUtc { get; init; }
    public string? LastMessage { get; init; }
}
