using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Xml.Linq;

namespace SupportTicket.DeploymentHelper.Plugins;

public sealed record PackagedAssembly(
    string Path,
    string Name,
    Version Version,
    bool IsSigned,
    long Size,
    IReadOnlyList<string> References,
    byte[] Sha256);

/// <summary>What a built plug-in package contains, read from the .nupkg without loading any code.</summary>
public sealed record PackageFacts(
    string Id,
    string Version,
    int DependencyCount,
    IReadOnlyList<string> Files,
    IReadOnlyList<PackagedAssembly> Assemblies);

public static class PluginPackageInspector
{
    public static PackageFacts Inspect(byte[] package)
    {
        using var zip = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);

        var nuspecEntry = zip.Entries.SingleOrDefault(e => !e.FullName.Contains('/') && e.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidDataException("The package has no .nuspec manifest.");
        XDocument nuspec;
        using (var stream = nuspecEntry.Open()) nuspec = XDocument.Load(stream);

        var metadata = nuspec.Root?.Elements().FirstOrDefault(e => e.Name.LocalName == "metadata")
            ?? throw new InvalidDataException("The .nuspec has no <metadata>.");
        string Value(string name) => metadata.Elements().FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? string.Empty;
        var dependencies = metadata.Descendants().Count(e => e.Name.LocalName == "dependency");

        var files = zip.Entries.Where(e => e.Length > 0 || !e.FullName.EndsWith('/')).Select(e => e.FullName).ToList();
        var assemblies = zip.Entries
            .Where(e => e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .Select(ReadAssembly)
            .ToList();

        return new PackageFacts(Value("id"), Value("version"), dependencies, files, assemblies);
    }

    private static PackagedAssembly ReadAssembly(ZipArchiveEntry entry)
    {
        using var buffer = new MemoryStream();
        using (var stream = entry.Open()) stream.CopyTo(buffer);
        buffer.Position = 0;

        var hash = System.Security.Cryptography.SHA256.HashData(buffer.ToArray());
        using var pe = new PEReader(buffer);
        if (!pe.HasMetadata) throw new InvalidDataException($"{entry.FullName} is not a .NET assembly.");

        var reader = pe.GetMetadataReader();
        var definition = reader.GetAssemblyDefinition();
        var references = reader.AssemblyReferences
            .Select(h => reader.GetString(reader.GetAssemblyReference(h).Name))
            .ToList();

        return new PackagedAssembly(
            entry.FullName,
            reader.GetString(definition.Name),
            definition.Version,
            !definition.PublicKey.IsNil,
            entry.Length,
            references,
            hash);
    }
}
