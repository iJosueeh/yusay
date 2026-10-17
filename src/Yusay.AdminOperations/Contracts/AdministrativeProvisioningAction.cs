namespace Yusay.AdminOperations.Contracts;

public enum AdministrativeProvisioningAction
{
    Provision,
    Revoke
}

public static class AdministrativeProvisioningActionExtensions
{
    public const string ProvisionDbValue = "ADMINISTRATOR_PROVISIONED";
    public const string RevokeDbValue = "ADMINISTRATOR_REVOKED";

    public static string ToDbValue(this AdministrativeProvisioningAction action) => action switch
    {
        AdministrativeProvisioningAction.Provision => ProvisionDbValue,
        AdministrativeProvisioningAction.Revoke => RevokeDbValue,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown administrative action.")
    };

    public static AdministrativeProvisioningAction FromDbValue(string dbValue) => dbValue switch
    {
        ProvisionDbValue => AdministrativeProvisioningAction.Provision,
        RevokeDbValue => AdministrativeProvisioningAction.Revoke,
        _ => throw new ArgumentException($"Invalid database action value: '{dbValue}'.", nameof(dbValue))
    };
}
