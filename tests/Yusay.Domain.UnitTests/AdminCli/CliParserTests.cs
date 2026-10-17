using Yusay.AdminCli.Parsing;

namespace Yusay.Domain.UnitTests.AdminCli;

public sealed class CliParserTests
{
    [Fact]
    public void Parse_Provision_WithValidArguments_ShouldReturnPopulatedCommand()
    {
        var userId = Guid.NewGuid();
        var eventId = Guid.NewGuid();

        var command = CliParser.Parse(new[]
        {
            "provision",
            "--user", userId.ToString(),
            "--operator", "sec_ops",
            "--ticket", "TICKET-123",
            "--event-id", eventId.ToString(),
            "--state-dir", "/tmp/state"
        });

        Assert.Equal(CommandType.Provision, command.Type);
        Assert.Equal(userId, command.TargetUserId);
        Assert.Equal("sec_ops", command.OperatorIdentifier);
        Assert.Equal("TICKET-123", command.TicketRef);
        Assert.Equal(eventId, command.EventId);
        Assert.Equal("/tmp/state", command.StateDirectory);
    }

    [Fact]
    public void Parse_Revoke_WithValidArguments_ShouldReturnPopulatedCommand()
    {
        var userId = Guid.NewGuid();

        var command = CliParser.Parse(new[]
        {
            "revoke",
            "--user", userId.ToString(),
            "--operator", "sec_ops"
        });

        Assert.Equal(CommandType.Revoke, command.Type);
        Assert.Equal(userId, command.TargetUserId);
        Assert.Equal("sec_ops", command.OperatorIdentifier);
        Assert.Null(command.TicketRef);
        Assert.Null(command.EventId);
    }

    [Fact]
    public void Parse_Verify_WithValidArguments_ShouldReturnPopulatedCommand()
    {
        var eventId = Guid.NewGuid();

        var command = CliParser.Parse(new[]
        {
            "verify",
            "--event-id", eventId.ToString()
        });

        Assert.Equal(CommandType.Verify, command.Type);
        Assert.Equal(eventId, command.EventId);
    }

    [Fact]
    public void Parse_ListPending_ShouldReturnListPendingCommand()
    {
        var command = CliParser.Parse(new[] { "list-pending" });
        Assert.Equal(CommandType.ListPending, command.Type);
    }

    [Theory]
    [InlineData("help")]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_HelpFlags_ShouldReturnHelpCommand(string flag)
    {
        var command = CliParser.Parse(new[] { flag });
        Assert.Equal(CommandType.Help, command.Type);
    }

    [Theory]
    [InlineData("--password")]
    [InlineData("-p")]
    [InlineData("--connection-string")]
    [InlineData("--conn")]
    [InlineData("--pwd")]
    [InlineData("--db-password")]
    public void Parse_WhenCredentialFlagPassed_ShouldThrowCliParseExceptionWithExitCode5(string forbiddenFlag)
    {
        var ex = Assert.Throws<CliParseException>(() => CliParser.Parse(new[]
        {
            "provision",
            "--user", Guid.NewGuid().ToString(),
            "--operator", "ops",
            forbiddenFlag, "super_secret"
        }));

        Assert.Equal(5, ex.ExitCode);
        Assert.Contains("Violación de seguridad", ex.Message);
    }

    [Fact]
    public void Parse_Provision_WhenOperatorMissing_ShouldThrowCliParseException()
    {
        var ex = Assert.Throws<CliParseException>(() => CliParser.Parse(new[]
        {
            "provision",
            "--user", Guid.NewGuid().ToString()
        }));

        Assert.Contains("--operator", ex.Message);
    }

    [Fact]
    public void Parse_Provision_WhenUserMissing_ShouldThrowCliParseException()
    {
        var ex = Assert.Throws<CliParseException>(() => CliParser.Parse(new[]
        {
            "provision",
            "--operator", "ops"
        }));

        Assert.Contains("--user", ex.Message);
    }

    [Fact]
    public void Parse_Verify_WhenEventIdMissing_ShouldThrowCliParseException()
    {
        var ex = Assert.Throws<CliParseException>(() => CliParser.Parse(new[]
        {
            "verify"
        }));

        Assert.Contains("--event-id", ex.Message);
    }

    [Fact]
    public void Parse_WhenConnectionStringPassedAsArgument_ThrowsSecurityExceptionWithoutEchoingSecret()
    {
        const string secretPassword = "SuperSecretPassword123!";
        var rawConnStr = $"Host=localhost;Database=yusay;Username=app;Password={secretPassword}";

        var ex = Assert.Throws<CliParseException>(() => CliParser.Parse(new[]
        {
            "provision",
            "--user", Guid.NewGuid().ToString(),
            "--operator", "ops",
            rawConnStr
        }));

        Assert.Equal(5, ex.ExitCode);
        Assert.Contains("Violación de seguridad", ex.Message);
        Assert.DoesNotContain(secretPassword, ex.Message);
    }

    [Fact]
    public void Parse_WhenCredentialEmbeddedInOptionValue_ThrowsSecurityExceptionWithoutEchoingSecret()
    {
        const string secret = "TopSecretPwd999";
        var ex = Assert.Throws<CliParseException>(() => CliParser.Parse(new[]
        {
            "provision",
            "--user", Guid.NewGuid().ToString(),
            "--operator", $"admin_user;Password={secret}"
        }));

        Assert.Equal(5, ex.ExitCode);
        Assert.Contains("Violación de seguridad", ex.Message);
        Assert.DoesNotContain(secret, ex.Message);
    }

    [Fact]
    public void Parse_WhenUnknownOptionPassed_DoesNotEchoOptionValueInErrorMessage()
    {
        const string sensitiveVal = "PotentialSecretValueXYZ";
        var ex = Assert.Throws<CliParseException>(() => CliParser.Parse(new[]
        {
            "provision",
            "--user", Guid.NewGuid().ToString(),
            "--operator", "ops",
            $"--unknown={sensitiveVal}"
        }));

        Assert.DoesNotContain(sensitiveVal, ex.Message);
        Assert.Contains("Opción desconocida o mal formateada", ex.Message);
    }
}
