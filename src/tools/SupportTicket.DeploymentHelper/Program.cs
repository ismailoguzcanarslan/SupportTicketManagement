// SupportTicket Deployment Helper: interactive, DEV only, nothing is written without confirmation.
// Usage: dotnet run --project src/tools/SupportTicket.DeploymentHelper [-- --dry-run]

using Microsoft.Extensions.DependencyInjection;
using SupportTicket.DeploymentHelper.AzureFunctions;
using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Frontend;
using SupportTicket.DeploymentHelper.Plugins;
using SupportTicket.DeploymentHelper.WebResources;
using SupportTicket.Tools.Dataverse;

var settingsPath = Path.Combine(AppContext.BaseDirectory, "deployment.settings.json");
var settings = DeploymentSettings.Load(settingsPath);
var manifest = DeploymentManifest.Load(Path.Combine(AppContext.BaseDirectory, settings.Manifest));
var repositoryRoot = RepositoryPaths.FindRoot(AppContext.BaseDirectory);
var options = new DeploymentRunOptions(DryRunOnly: args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase));

var validator = new ManifestValidator(settings.PublisherPrefix, repositoryRoot);

var services = new ServiceCollection()
    .AddSingleton(settings)
    .AddSingleton(manifest)
    .AddSingleton(validator)
    .AddSingleton<IUserPrompt, ConsoleUserPrompt>()
    .AddSingleton<IProcessRunner, ProcessRunner>()
    .AddSingleton<IFrontendBuild>(provider => new FrontendBuild(provider.GetRequiredService<IProcessRunner>(), validator, repositoryRoot))
    .AddSingleton<DataverseConnectionService>()
    .AddSingleton<DataverseSession>()
    .AddSingleton<IDataverseSession>(provider => provider.GetRequiredService<DataverseSession>())
    .AddSingleton<SolutionRepository>()
    .AddSingleton<PluginStepDiscovery>()
    .AddSingleton<PluginRegistrationRepository>()
    .AddSingleton<WebResourceRepository>()
    .AddSingleton<IDeploymentTarget, WebResourceDeploymentTarget>()
    .AddSingleton<IDeploymentTarget, PluginDeploymentTarget>()
    .AddSingleton<IDeploymentTarget, AzureFunctionsDeploymentTarget>()
    .AddSingleton<DeploymentConsole>();

using var provider = services.BuildServiceProvider();

if (options.DryRunOnly) Console.WriteLine("Dry-run mode: plans are shown, nothing is written.");

return await provider.GetRequiredService<DeploymentConsole>().RunAsync(options, CancellationToken.None);
