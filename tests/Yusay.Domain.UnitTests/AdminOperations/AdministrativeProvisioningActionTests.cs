using Yusay.AdminOperations.Contracts;

namespace Yusay.Domain.UnitTests.AdminOperations;

public sealed class AdministrativeProvisioningActionTests
{
    [Fact]
    public void ToDbValue_ShouldMapToNormativeDatabaseConstants()
    {
        Assert.Equal("ADMINISTRATOR_PROVISIONED", AdministrativeProvisioningAction.Provision.ToDbValue());
        Assert.Equal("ADMINISTRATOR_REVOKED", AdministrativeProvisioningAction.Revoke.ToDbValue());
    }

    [Fact]
    public void FromDbValue_ShouldMapFromNormativeDatabaseConstants()
    {
        Assert.Equal(AdministrativeProvisioningAction.Provision, AdministrativeProvisioningActionExtensions.FromDbValue("ADMINISTRATOR_PROVISIONED"));
        Assert.Equal(AdministrativeProvisioningAction.Revoke, AdministrativeProvisioningActionExtensions.FromDbValue("ADMINISTRATOR_REVOKED"));
    }

    [Fact]
    public void FromDbValue_WithInvalidValue_ShouldThrowArgumentException()
    {
        Assert.Throws<ArgumentException>(() => AdministrativeProvisioningActionExtensions.FromDbValue("ALTA"));
        Assert.Throws<ArgumentException>(() => AdministrativeProvisioningActionExtensions.FromDbValue("BAJA"));
    }
}
