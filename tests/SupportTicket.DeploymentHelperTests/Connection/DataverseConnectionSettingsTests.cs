using SupportTicket.Tools.Dataverse;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Connection;

// Tests the connection configuration shared by EntityGenerator and DeploymentHelper.
public class DataverseConnectionSettingsTests
{
    private const string ClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

    [Fact]
    public void Load_ReadsAllOgzDataverseVariables()
    {
        var settings = Load(new()
        {
            ["OGZ_DATAVERSE_URL"] = " https://contoso-dev.crm4.dynamics.com/main.aspx ",
            ["OGZ_DATAVERSE_CLIENT_ID"] = ClientId,
            ["OGZ_DATAVERSE_AUTH_TYPE"] = "interactive",
            ["OGZ_DATAVERSE_REDIRECT_URI"] = "http://localhost:5050",
            ["OGZ_DATAVERSE_TENANT_ID"] = "tenant",
        });

        Assert.Equal("https://contoso-dev.crm4.dynamics.com", settings.EnvironmentUrl);
        Assert.Equal(ClientId, settings.ClientId);
        Assert.Equal("http://localhost:5050", settings.RedirectUri);
        Assert.Equal("tenant", settings.TenantId);
        Assert.Empty(settings.Validate());
    }

    [Fact]
    public void Load_DefaultsToInteractiveAndTreatsBlankAsMissing()
    {
        var settings = Load(new() { ["OGZ_DATAVERSE_URL"] = "https://contoso-dev.crm4.dynamics.com", ["OGZ_DATAVERSE_CLIENT_ID"] = ClientId, ["OGZ_DATAVERSE_REDIRECT_URI"] = "  " });

        Assert.Equal("Interactive", settings.AuthType);
        Assert.Null(settings.RedirectUri);
        Assert.Empty(settings.Validate());
    }

    [Fact]
    public void Validate_NamesEveryMissingVariable()
    {
        var errors = Load(new()).Validate();

        Assert.Contains(errors, e => e.StartsWith("OGZ_DATAVERSE_URL is not set"));
        Assert.Contains(errors, e => e.StartsWith("OGZ_DATAVERSE_CLIENT_ID is not set"));
    }

    [Theory]
    [InlineData("contoso-dev.crm4.dynamics.com")]
    [InlineData("http://contoso-dev.crm4.dynamics.com")]
    [InlineData("not a url")]
    public void Validate_RejectsUrlsThatAreNotAbsoluteHttps(string url)
    {
        var errors = Load(new() { ["OGZ_DATAVERSE_URL"] = url, ["OGZ_DATAVERSE_CLIENT_ID"] = ClientId }).Validate();

        Assert.Contains(errors, e => e.StartsWith("OGZ_DATAVERSE_URL") && e.Contains("not a valid https URL"));
    }

    [Fact]
    public void Validate_RejectsNonGuidClientIdAndUnsupportedAuthType()
    {
        var errors = Load(new()
        {
            ["OGZ_DATAVERSE_URL"] = "https://contoso-dev.crm4.dynamics.com",
            ["OGZ_DATAVERSE_CLIENT_ID"] = "my-app",
            ["OGZ_DATAVERSE_AUTH_TYPE"] = "ClientSecret",
        }).Validate();

        Assert.Contains(errors, e => e.StartsWith("OGZ_DATAVERSE_CLIENT_ID") && e.Contains("not a GUID"));
        Assert.Contains(errors, e => e.StartsWith("OGZ_DATAVERSE_AUTH_TYPE") && e.Contains("not supported"));
    }

    [Fact]
    public void Connect_ThrowsTheValidationErrorsWithoutOpeningASignIn()
    {
        var error = Assert.Throws<InvalidOperationException>(() => new DataverseConnectionService().Connect(Load(new())));

        Assert.Contains("Dataverse connection is not configured", error.Message);
        Assert.Contains("OGZ_DATAVERSE_URL is not set", error.Message);
    }

    private static DataverseConnectionSettings Load(Dictionary<string, string> variables) =>
        DataverseConnectionSettingsLoader.Load(name => variables.GetValueOrDefault(name));
}
