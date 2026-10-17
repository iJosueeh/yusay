using Yusay.AdminOperations.Contracts;

namespace Yusay.Domain.UnitTests.AdminOperations;

public sealed class PayloadComparatorTests
{
    [Fact]
    public void Matches_WhenAllFourFieldsMatch_ShouldReturnTrue()
    {
        var targetUserId = Guid.NewGuid();
        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_operator",
            "SEC-999");

        var matches = PayloadComparator.Matches(
            targetUserId,
            "ADMINISTRATOR_PROVISIONED",
            "ops_operator",
            "SEC-999",
            request);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_WhenBothTicketRefsAreNull_ShouldReturnTrue()
    {
        var targetUserId = Guid.NewGuid();
        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            targetUserId,
            AdministrativeProvisioningAction.Revoke,
            "ops_operator",
            ticketRef: null);

        var matches = PayloadComparator.Matches(
            targetUserId,
            "ADMINISTRATOR_REVOKED",
            "ops_operator",
            persistedTicketRef: null,
            request);

        Assert.True(matches);
    }

    [Fact]
    public void Matches_WhenTargetUserIdDiffers_ShouldReturnFalse()
    {
        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdministrativeProvisioningAction.Provision,
            "ops_operator",
            "SEC-1");

        var matches = PayloadComparator.Matches(
            Guid.NewGuid(), // Different user
            "ADMINISTRATOR_PROVISIONED",
            "ops_operator",
            "SEC-1",
            request);

        Assert.False(matches);
    }

    [Fact]
    public void Matches_WhenActionDiffers_ShouldReturnFalse()
    {
        var targetUserId = Guid.NewGuid();
        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_operator",
            "SEC-1");

        var matches = PayloadComparator.Matches(
            targetUserId,
            "ADMINISTRATOR_REVOKED", // Different action
            "ops_operator",
            "SEC-1",
            request);

        Assert.False(matches);
    }

    [Fact]
    public void Matches_WhenOperatorIdentifierDiffers_ShouldReturnFalse()
    {
        var targetUserId = Guid.NewGuid();
        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_operator_1",
            "SEC-1");

        var matches = PayloadComparator.Matches(
            targetUserId,
            "ADMINISTRATOR_PROVISIONED",
            "ops_operator_2", // Different operator
            "SEC-1",
            request);

        Assert.False(matches);
    }

    [Theory]
    [InlineData(null, "SEC-1")]
    [InlineData("SEC-1", null)]
    [InlineData("SEC-1", "SEC-2")]
    public void Matches_WhenTicketRefDiffers_ShouldReturnFalse(string? persistedTicket, string? requestTicket)
    {
        var targetUserId = Guid.NewGuid();
        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            targetUserId,
            AdministrativeProvisioningAction.Provision,
            "ops_operator",
            requestTicket);

        var matches = PayloadComparator.Matches(
            targetUserId,
            "ADMINISTRATOR_PROVISIONED",
            "ops_operator",
            persistedTicket,
            request);

        Assert.False(matches);
    }
}
