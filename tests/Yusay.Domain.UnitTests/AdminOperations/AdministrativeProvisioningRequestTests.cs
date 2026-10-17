using Yusay.AdminOperations.Contracts;

namespace Yusay.Domain.UnitTests.AdminOperations;

public sealed class AdministrativeProvisioningRequestTests
{
    [Fact]
    public void Constructor_WithValidArguments_ShouldInstantiateAndNormalize()
    {
        var eventId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var request = new AdministrativeProvisioningRequest(
            eventId,
            userId,
            AdministrativeProvisioningAction.Provision,
            "  ops_operator  ",
            "  SEC-123  ");

        Assert.Equal(eventId, request.EventId);
        Assert.Equal(userId, request.TargetUserId);
        Assert.Equal(AdministrativeProvisioningAction.Provision, request.Action);
        Assert.Equal("ops_operator", request.OperatorIdentifier);
        Assert.Equal("SEC-123", request.TicketRef);
    }

    [Fact]
    public void Constructor_WithEmptyTicketRef_ShouldNormalizeToNull()
    {
        var request = new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdministrativeProvisioningAction.Revoke,
            "ops_operator",
            "   ");

        Assert.Null(request.TicketRef);
    }

    [Fact]
    public void Constructor_WithEmptyEventId_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new AdministrativeProvisioningRequest(
            Guid.Empty,
            Guid.NewGuid(),
            AdministrativeProvisioningAction.Provision,
            "ops_operator"));

        Assert.Contains("event_id", ex.Message);
    }

    [Fact]
    public void Constructor_WithEmptyTargetUserId_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            Guid.Empty,
            AdministrativeProvisioningAction.Provision,
            "ops_operator"));

        Assert.Contains("target_user_id", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyOrWhitespaceOperator_ShouldThrowArgumentException(string? invalidOperator)
    {
        var ex = Assert.Throws<ArgumentException>(() => new AdministrativeProvisioningRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            AdministrativeProvisioningAction.Provision,
            invalidOperator!));

        Assert.Contains("operator_identifier", ex.Message);
    }
}
