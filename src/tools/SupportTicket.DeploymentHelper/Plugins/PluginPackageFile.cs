namespace SupportTicket.DeploymentHelper.Plugins;

/// <summary>The built {PackageId}.{Version}.nupkg to deploy.</summary>
public sealed record PluginPackageFile(string Path, string Version, byte[] Content)
{
    /// <summary>Picks the highest version of the package in the folder (old builds may still be there).</summary>
    public static PluginPackageFile FindLatest(string directory, string packageId)
    {
        var prefix = packageId + ".";
        var candidates = Directory.GetFiles(directory, packageId + ".*.nupkg")
            .Select(path => (Path: path, Version: System.IO.Path.GetFileNameWithoutExtension(path)[prefix.Length..]))
            .Select(c => (c.Path, c.Version, Parsed: System.Version.TryParse(c.Version, out var v) ? v : null))
            .Where(c => c.Parsed is not null)
            .OrderByDescending(c => c.Parsed)
            .ToList();

        if (candidates.Count == 0)
            throw new FileNotFoundException($"No {packageId}.<version>.nupkg in {directory}. Build SupportTicket.Plugins in Release first.");

        var latest = candidates[0];
        return new PluginPackageFile(latest.Path, latest.Version, File.ReadAllBytes(latest.Path));
    }
}
