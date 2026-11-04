using System.Reflection;
using System.Security.Cryptography;
using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Plugins;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Plugins;

public class PluginPackageValidatorTests
{
    private const string PackageId = "ogz_SupportTicketPlugins";
    private static readonly byte[] PluginBytes = [1, 2, 3];

    [Fact]
    public void BuiltPackage_MeetsDataverseRequirements()
    {
        var binDirectory = Path.Combine(RepositoryPaths.FindRoot(AppContext.BaseDirectory),
            "src", "dataverse", "SupportTicket.Plugins", "bin", Configuration());
        var package = PluginPackageFile.FindLatest(binDirectory, PackageId);
        var assembly = File.ReadAllBytes(Path.Combine(binDirectory, "net462", "SupportTicket.Plugins.dll"));

        var facts = PluginPackageInspector.Inspect(package.Content);

        Assert.Empty(PluginPackageValidator.Validate(facts, PackageId, package.Version, "SupportTicket.Plugins", assembly));
        Assert.Equal(
            ["SupportTicket.Base", "SupportTicket.BusinessLogic", "SupportTicket.Entities", "SupportTicket.Plugins"],
            facts.Assemblies.Select(a => a.Name).Order());
        Assert.All(facts.Assemblies, a => Assert.Equal(new Version(1, 0, 0, 0), a.Version));
        Assert.All(facts.Assemblies, a => Assert.False(a.IsSigned));
    }

    [Fact]
    public void ValidFacts_HaveNoErrors()
    {
        Assert.Empty(Validate(Facts()));
    }

    [Fact]
    public void WrongIdDependenciesAndFramework_AreRejected()
    {
        var facts = Facts() with { Id = "SupportTicketPlugins", DependencyCount = 2, Files = ["lib/net8.0/SupportTicket.Plugins.dll"] };

        var errors = Validate(facts);

        Assert.Contains(errors, e => e.Contains("expected 'ogz_SupportTicketPlugins'"));
        Assert.Contains(errors, e => e.Contains("NuGet dependencies"));
        Assert.Contains(errors, e => e.Contains("must target .NET Framework 4.6.2"));
    }

    [Fact]
    public void SdkAssembliesInPackage_AreRejected()
    {
        var facts = Facts() with { Assemblies = [.. Facts().Assemblies, Assembly("Microsoft.Xrm.Sdk")] };

        Assert.Contains(Validate(facts), e => e.Contains("provided by the Dataverse sandbox"));
    }

    [Fact]
    public void WorkflowActivitiesAndOversizedAssemblies_AreRejected()
    {
        var facts = Facts() with
        {
            Assemblies = [Plugin() with { References = ["mscorlib", "System.Activities"], Size = PluginPackageValidator.MaxAssemblySize + 1 }],
        };

        var errors = Validate(facts);

        Assert.Contains(errors, e => e.Contains("custom workflow activities are not supported"));
        Assert.Contains(errors, e => e.Contains("limited to 16 MB"));
    }

    [Fact]
    public void UsingAnExcludedSdkDependency_IsRejectedUnlessPacked()
    {
        var usesJson = Facts() with { Assemblies = [Plugin() with { References = ["mscorlib", "System.Text.Json"] }] };
        var packsJson = usesJson with { Assemblies = [.. usesJson.Assemblies, Assembly("System.Text.Json")] };

        Assert.Contains(Validate(usesJson), e => e.Contains("System.Text.Json, which is not in the package"));
        Assert.DoesNotContain(Validate(packsJson), e => e.Contains("System.Text.Json"));
    }

    [Fact]
    public void SignedAssemblyDependingOnUnsigned_IsRejected()
    {
        var facts = Facts() with
        {
            Assemblies = [Plugin() with { IsSigned = true, References = ["mscorlib", "SupportTicket.Base"] }, Assembly("SupportTicket.Base")],
        };

        Assert.Contains(Validate(facts), e => e.Contains("signed but depends on unsigned SupportTicket.Base.dll"));
    }

    [Fact]
    public void PackagedPluginFromAnotherBuild_IsRejected()
    {
        var errors = PluginPackageValidator.Validate(Facts(), PackageId, "1.0.0", "SupportTicket.Plugins", [9, 9, 9]);

        Assert.Contains(errors, e => e.Contains("differs from the built assembly"));
    }

    [Fact]
    public void FileNameVersionMustMatchNuspec()
    {
        Assert.Contains(PluginPackageValidator.Validate(Facts(), PackageId, "1.0.1", "SupportTicket.Plugins", PluginBytes),
            e => e.Contains("file name says version 1.0.1"));
    }

    private static IReadOnlyList<string> Validate(PackageFacts facts) =>
        PluginPackageValidator.Validate(facts, PackageId, "1.0.0", "SupportTicket.Plugins", PluginBytes);

    private static PackageFacts Facts() => new(PackageId, "1.0.0", 0, ["lib/net462/SupportTicket.Plugins.dll"], [Plugin()]);

    private static PackagedAssembly Plugin() => Assembly("SupportTicket.Plugins") with { Sha256 = SHA256.HashData(PluginBytes) };

    private static PackagedAssembly Assembly(string name) =>
        new($"lib/net462/{name}.dll", name, new Version(1, 0, 0, 0), false, 1024, ["mscorlib"], []);

    private static string Configuration() =>
        typeof(PluginPackageValidatorTests).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()!.Configuration;
}
