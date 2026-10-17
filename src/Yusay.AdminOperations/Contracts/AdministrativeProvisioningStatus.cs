namespace Yusay.AdminOperations.Contracts;

public enum AdministrativeProvisioningStatus
{
    Success,
    PreviouslyApplied,
    PreconditionFailed,
    CollisionConflict,
    VerificationPending,
    Failed
}
