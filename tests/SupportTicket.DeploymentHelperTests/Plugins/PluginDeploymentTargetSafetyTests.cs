using System.Reflection;
using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Plugins;
using SupportTicket.DeploymentHelperTests.Fakes;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Plugins;

// Offline checks of option 2 must stop the pipeline before Dataverse is touched.
public sealed class PluginDeploymentTargetSafetyTests : IDisposable
{
    private const string PackageId = "ogz_SupportTicketPlugins";
    private readonly TestRepository _repo = new();
    private readonly InMemoryDataverse _dataverse = new();

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task InvalidManifest_StopsBeforeConnecting()
    {
        var prompt = new RecordingPrompt();
        var manifest = new DeploymentManifest { Plugins = new PluginsEntry { PackageId = PackageId, Assembly = "missing.dll", PackageDirectory = "missing" } };

        await Target(manifest, prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(0, _dataverse.Calls);
        Assert.Contains("Manifest is not valid; nothing was deployed:", prompt.Output);
    }

    [Fact]
    public async Task StalePackage_FromAnotherBuild_StopsBeforeConnecting()
    {
        // The real package, next to a plug-in DLL from "another build".
        var built = Path.Combine(RepositoryPaths.FindRoot(AppContext.BaseDirectory), "src", "dataverse", "SupportTicket.Plugins", "bin", Configuration());
        var package = PluginPackageFile.FindLatest(built, PackageId);
        File.Copy(package.Path, Path.Combine(Directory.CreateDirectory(Path.Combine(_repo.Root, "bin")).FullName, Path.GetFileName(package.Path)));
        _repo.WriteFile("bin/net462/SupportTicket.Plugins.dll", "not the packaged assembly");
        var prompt = new RecordingPrompt();
        var manifest = new DeploymentManifest
        {
            Plugins = new PluginsEntry { PackageId = PackageId, Assembly = "bin/net462/SupportTicket.Plugins.dll", PackageDirectory = "bin" },
        };

        await Target(manifest, prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(0, _dataverse.Calls);
        Assert.Contains(prompt.Output, l => l.Contains("differs from the built assembly"));
    }

    private PluginDeploymentTarget Target(DeploymentManifest manifest, RecordingPrompt prompt)
    {
        var session = new FakeSession(_dataverse);
        return new PluginDeploymentTarget(
            new DeploymentSettings { SolutionUniqueName = "OguzLabsSupportTicketManagement", PublisherPrefix = "ogz" },
            manifest,
            new ManifestValidator("ogz", _repo.Root),
            new PluginStepDiscovery(),
            new PluginRegistrationRepository(session),
            new SolutionRepository(session),
            session,
            prompt);
    }

    private static string Configuration() =>
        typeof(PluginDeploymentTargetSafetyTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;

    private sealed class RecordingPrompt : IUserPrompt
    {
        public List<string> Output { get; } = [];

        public void WriteLine(string text = "") => Output.Add(text);

        public string? ReadLine(string prompt) => null;
    }
}
