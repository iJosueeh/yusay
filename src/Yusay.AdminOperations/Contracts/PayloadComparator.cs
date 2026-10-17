namespace Yusay.AdminOperations.Contracts;

public static class PayloadComparator
{
    public static bool Matches(
        Guid persistedTargetUserId,
        string persistedAction,
        string persistedOperatorIdentifier,
        string? persistedTicketRef,
        AdministrativeProvisioningRequest request)
    {
        if (persistedTargetUserId != request.TargetUserId)
            return false;

        if (!string.Equals(persistedAction, request.Action.ToDbValue(), StringComparison.Ordinal))
            return false;

        if (!string.Equals(persistedOperatorIdentifier?.Trim(), request.OperatorIdentifier, StringComparison.Ordinal))
            return false;

        var normalizedPersistedTicket = string.IsNullOrWhiteSpace(persistedTicketRef) ? null : persistedTicketRef.Trim();
        return string.Equals(normalizedPersistedTicket, request.TicketRef, StringComparison.Ordinal);
    }
}
