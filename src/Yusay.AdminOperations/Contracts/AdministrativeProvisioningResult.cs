namespace Yusay.AdminOperations.Contracts;

public sealed record AdministrativeProvisioningResult(
    AdministrativeProvisioningStatus Status,
    Guid EventId,
    Guid TargetUserId,
    string Action,
    string OperatorIdentifier,
    string? TicketRef,
    string? Message = null,
    DateTimeOffset? OccurredAt = null)
{
    public bool IsSuccess => Status is AdministrativeProvisioningStatus.Success or AdministrativeProvisioningStatus.PreviouslyApplied;
}
