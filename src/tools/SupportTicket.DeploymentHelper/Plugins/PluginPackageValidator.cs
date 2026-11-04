namespace SupportTicket.DeploymentHelper.Plugins;

/// <summary>
/// Offline checks of a built plug-in package against Dataverse's documented requirements
/// ("Build and package plug-in code", learn.microsoft.com). Pure: works on <see cref="PackageFacts"/>.
/// </summary>
public static class PluginPackageValidator
{
    public const long MaxAssemblySize = 16L * 1024 * 1024;

    private const string LibFolder = "lib/net462/";

    // Provided by the sandbox; must not be uploaded.
    private static readonly HashSet<string> PlatformAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft.Xrm.Sdk", "Microsoft.Crm.Sdk.Proxy", "Microsoft.Xrm.Sdk.Workflow",
    };

    // Dependencies of the Dataverse SDK packages that are excluded from our package (see
    // SupportTicket.Plugins.csproj). The sandbox's copies may be other versions, so code must not use
    // them unless they are packed deliberately.
    private static readonly HashSet<string> ExcludedSdkDependencies = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.Text.Json", "System.Text.Encodings.Web", "System.Memory", "System.Buffers",
        "System.Numerics.Vectors", "System.Runtime.CompilerServices.Unsafe", "System.Threading.Tasks.Extensions",
        "Microsoft.Bcl.AsyncInterfaces", "System.ServiceModel.Primitives", "System.ServiceModel.Http",
    };

    /// <param name="pluginAssemblyFile">The built plug-in DLL whose attributes are deployed; must be the one inside the package.</param>
    public static IReadOnlyList<string> Validate(PackageFacts package, string expectedPackageId, string fileVersion, string pluginAssemblyName, byte[] pluginAssemblyFile)
    {
        var errors = new List<string>();

        if (!string.Equals(package.Version, fileVersion, StringComparison.Ordinal))
            errors.Add($"Package file name says version {fileVersion}, but its .nuspec says {package.Version}.");

        var packagedPlugin = package.Assemblies.FirstOrDefault(a => string.Equals(a.Name, pluginAssemblyName, StringComparison.OrdinalIgnoreCase));
        if (packagedPlugin is not null && !packagedPlugin.Sha256.AsSpan().SequenceEqual(System.Security.Cryptography.SHA256.HashData(pluginAssemblyFile)))
            errors.Add($"{pluginAssemblyName}.dll in the package differs from the built assembly (manifest plugins.assembly). Rebuild so both come from the same build.");

        if (!string.Equals(package.Id, expectedPackageId, StringComparison.Ordinal))
            errors.Add($"Package id is '{package.Id}', expected '{expectedPackageId}' (manifest plugins.packageId).");

        if (string.IsNullOrWhiteSpace(package.Version))
            errors.Add("Package has no version.");

        if (package.DependencyCount > 0)
            errors.Add($"Package declares {package.DependencyCount} NuGet dependencies; dependent assemblies must be inside the package.");

        foreach (var file in package.Files.Where(f => f.StartsWith("lib/", StringComparison.OrdinalIgnoreCase)
                                                       && !f.StartsWith(LibFolder, StringComparison.OrdinalIgnoreCase)))
            errors.Add($"{file}: plug-in packages must target .NET Framework 4.6.2 ({LibFolder}).");

        if (!package.Assemblies.Any(a => string.Equals(a.Name, pluginAssemblyName, StringComparison.OrdinalIgnoreCase)))
            errors.Add($"Package does not contain the plug-in assembly {pluginAssemblyName}.dll.");

        var packaged = package.Assemblies.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var assembly in package.Assemblies)
        {
            if (PlatformAssemblies.Contains(assembly.Name))
                errors.Add($"{assembly.Name}.dll is provided by the Dataverse sandbox and must not be in the package.");

            if (assembly.Size > MaxAssemblySize)
                errors.Add($"{assembly.Name}.dll is {assembly.Size / 1024 / 1024} MB; assemblies are limited to 16 MB.");

            if (assembly.References.Contains("System.Activities", StringComparer.OrdinalIgnoreCase))
                errors.Add($"{assembly.Name}.dll references System.Activities; custom workflow activities are not supported in plug-in packages.");

            foreach (var reference in assembly.References.Where(r => ExcludedSdkDependencies.Contains(r) && !packaged.ContainsKey(r)))
                errors.Add($"{assembly.Name}.dll uses {reference}, which is not in the package; the sandbox copy may be a different version. Pack it deliberately or remove the dependency.");

            if (assembly.IsSigned)
            {
                foreach (var unsigned in assembly.References.Where(r => packaged.TryGetValue(r, out var dependency) && !dependency.IsSigned))
                    errors.Add($"{assembly.Name}.dll is signed but depends on unsigned {unsigned}.dll; signed assemblies cannot load unsigned ones.");
            }
        }

        return errors;
    }
}
