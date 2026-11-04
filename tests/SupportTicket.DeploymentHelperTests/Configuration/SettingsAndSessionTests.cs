using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.Tools.Dataverse;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Configuration;

public class SettingsAndSessionTests
{
    private static readonly DataverseEnvironmentInfo Connected =
        new("Contoso DEV", "orgdev", "https://contoso-dev.crm4.dynamics.com", Guid.NewGuid(), Guid.NewGuid());

    [Theory]
    [InlineData("TEST")]
    [InlineData("UAT")]
    [InlineData("PROD")]
    [InlineData("dev")]
    public void NonDevTargetEnvironment_IsRefused(string environment)
    {
        Assert.Throws<InvalidOperationException>(() => new DeploymentSettings { TargetEnvironment = environment }.EnsureTargetIsDev());
    }

    [Fact]
    public void DevTarget_NeedsNoEnvironmentIdentitySettings()
    {
        new DeploymentSettings { TargetEnvironment = "DEV" }.EnsureTargetIsDev();
    }

    [Fact]
    public void ConnectionSummary_ShowsEnvironmentUrlAndWhatIsNotVerified()
    {
        var lines = DataverseSession.DescribeConnection("DEV", Connected).ToList();

        Assert.Contains("  Environment:  DEV", lines);
        Assert.Contains("  Dataverse:    Contoso DEV (orgdev)", lines);
        Assert.Contains("  URL:          https://contoso-dev.crm4.dynamics.com", lines);
        Assert.Contains(lines, l => l.Contains("deploys to whatever OGZ_DATAVERSE_URL points to"));
        Assert.DoesNotContain(lines, l => l.Contains("verified", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Session_ReportsMissingVariablesBeforeConnecting()
    {
        var session = new DataverseSession(new DeploymentSettings(), new DataverseConnectionService(), new NullPrompt(),
            () => DataverseConnectionSettingsLoader.Load(_ => null));

        var error = Assert.Throws<InvalidOperationException>(() => session.LoadConnectionSettings());

        Assert.Contains("OGZ_DATAVERSE_URL is not set", error.Message);
        Assert.Contains("OGZ_DATAVERSE_CLIENT_ID is not set", error.Message);
    }

    [Fact]
    public void Session_RefusesNonDevTargetBeforeReadingVariables()
    {
        var session = new DataverseSession(new DeploymentSettings { TargetEnvironment = "PROD" }, new DataverseConnectionService(), new NullPrompt(),
            () => throw new InvalidOperationException("variables must not be read"));

        var error = Assert.Throws<InvalidOperationException>(() => session.LoadConnectionSettings());

        Assert.Contains("only deploys to DEV", error.Message);
    }

    [Fact]
    public void CommittedSettings_TargetDevMainSolution_WithoutUrlIdentityOrSecrets()
    {
        var root = RepositoryPaths.FindRoot(AppContext.BaseDirectory);
        var path = Path.Combine(root, "src", "tools", "SupportTicket.DeploymentHelper", "deployment.settings.json");
        var text = File.ReadAllText(path);
        var settings = DeploymentSettings.Load(path);

        Assert.Equal("DEV", settings.TargetEnvironment);
        Assert.Equal("OguzLabsSupportTicketManagement", settings.SolutionUniqueName);
        Assert.Equal("ogz", settings.PublisherPrefix);
        Assert.DoesNotContain("://", text);
        Assert.DoesNotContain("organizationId", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class NullPrompt : IUserPrompt
    {
        public void WriteLine(string text = "") { }

        public string? ReadLine(string prompt) => null;
    }
}
