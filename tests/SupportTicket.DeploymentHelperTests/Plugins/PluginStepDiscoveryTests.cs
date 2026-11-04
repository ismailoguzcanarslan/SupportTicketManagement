using System.Reflection;
using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Plugins;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Plugins;

// Reads the real, built SupportTicket.Plugins.dll (net462) the same way the deployment does.
public class PluginStepDiscoveryTests
{
    private const string ValidationPlugin = "SupportTicket.Plugins.SupportTickets.SupportTicketValidationPlugin";

    [Fact]
    public void DiscoversDeclaredStepsFromBuiltPluginAssembly()
    {
        var result = new PluginStepDiscovery().Discover(PluginAssemblyPath());

        Assert.Empty(result.Errors);
        Assert.Contains(ValidationPlugin, result.PluginTypeNames);
        var steps = result.Steps.Where(s => s.PluginTypeName == ValidationPlugin).OrderBy(s => s.MessageName).ToList();
        Assert.Equal(2, steps.Count);

        var create = steps[0];
        Assert.Equal("Create", create.MessageName);
        Assert.Equal("ogz_support_ticket", create.EntityLogicalName);
        Assert.Equal(StepStage.PreValidation, create.Stage);
        Assert.False(create.Async);
        Assert.Equal(1, create.ExecutionOrder);
        Assert.Null(create.FilteringAttributes);

        var update = steps[1];
        Assert.Equal("Update", update.MessageName);
        Assert.Equal("ogz_ticket_title", update.FilteringAttributes);
        Assert.Equal($"{ValidationPlugin}: Update of ogz_support_ticket (PreValidation)", update.Name);
    }

    [Fact]
    public void DiscoveredSteps_PassValidation()
    {
        var result = new PluginStepDiscovery().Discover(PluginAssemblyPath());

        Assert.Empty(StepValidator.Validate(result.Steps));
    }

    private static string PluginAssemblyPath()
    {
        var configuration = typeof(PluginStepDiscoveryTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
        var path = Path.Combine(RepositoryPaths.FindRoot(AppContext.BaseDirectory),
            "src", "dataverse", "SupportTicket.Plugins", "bin", configuration, "net462", "SupportTicket.Plugins.dll");
        Assert.True(File.Exists(path), $"Build SupportTicket.Plugins ({configuration}) first: {path}");
        return path;
    }
}
