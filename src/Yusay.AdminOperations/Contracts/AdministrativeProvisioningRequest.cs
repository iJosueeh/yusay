namespace Yusay.AdminOperations.Contracts;

public sealed record AdministrativeProvisioningRequest
{
    public Guid EventId { get; }
    public Guid TargetUserId { get; }
    public AdministrativeProvisioningAction Action { get; }
    public string OperatorIdentifier { get; }
    public string? TicketRef { get; }

    public AdministrativeProvisioningRequest(
        Guid eventId,
        Guid targetUserId,
        AdministrativeProvisioningAction action,
        string operatorIdentifier,
        string? ticketRef = null)
    {
        if (eventId == Guid.Empty)
            throw new ArgumentException("event_id must not be empty.", nameof(eventId));

        if (targetUserId == Guid.Empty)
            throw new ArgumentException("target_user_id must not be empty.", nameof(targetUserId));

        if (string.IsNullOrWhiteSpace(operatorIdentifier))
            throw new ArgumentException("operator_identifier must not be empty or whitespace.", nameof(operatorIdentifier));

        if (!Enum.IsDefined(typeof(AdministrativeProvisioningAction), action))
            throw new ArgumentOutOfRangeException(nameof(action), action, "Invalid administrative action.");

        EventId = eventId;
        TargetUserId = targetUserId;
        Action = action;
        OperatorIdentifier = operatorIdentifier.Trim();
        TicketRef = string.IsNullOrWhiteSpace(ticketRef) ? null : ticketRef.Trim();
    }
}
