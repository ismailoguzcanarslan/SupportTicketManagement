using System.Text.RegularExpressions;
using SupportTicket.DeploymentHelper.WebResources;

namespace SupportTicket.DeploymentHelper.Configuration;

/// <summary>Validates the manifest before anything connects to Dataverse.</summary>
public sealed partial class ManifestValidator
{
    private static readonly HashSet<string> SupportedFormEvents = new(StringComparer.Ordinal) { "onload", "onsave" };

    private readonly string _publisherPrefix;
    private readonly string _repositoryRoot;

    public ManifestValidator(string publisherPrefix, string repositoryRoot)
    {
        _publisherPrefix = publisherPrefix;
        _repositoryRoot = Path.GetFullPath(repositoryRoot);
    }

    public IReadOnlyList<string> ValidateWebResources(DeploymentManifest manifest)
    {
        var errors = new List<string>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in manifest.WebResources)
        {
            var label = string.IsNullOrWhiteSpace(entry.Name) ? "(unnamed web resource)" : entry.Name;

            if (!WebResourceNamePattern().IsMatch(entry.Name))
                errors.Add($"{label}: name may contain only letters, digits, '_', '-', '.' and '/'.");
            else if (!entry.Name.StartsWith(_publisherPrefix + "_/", StringComparison.Ordinal))
                errors.Add($"{label}: name must start with '{_publisherPrefix}_/'.");

            if (!names.Add(entry.Name))
                errors.Add($"{label}: listed more than once.");

            if (WebResourceType.FromFileName(entry.Name) is null)
                errors.Add($"{label}: unsupported file extension.");

            if (!string.Equals(Path.GetExtension(entry.Name), Path.GetExtension(entry.File), StringComparison.OrdinalIgnoreCase))
                errors.Add($"{label}: file extension does not match the web resource name ({entry.File}).");

            var fullPath = ResolveInRepository(entry.File);
            if (fullPath is null)
                errors.Add($"{label}: file path must stay inside the repository ({entry.File}).");
            else if (!File.Exists(fullPath))
                errors.Add($"{label}: file not found, build it first ({entry.File}).");
        }

        foreach (var form in manifest.Forms)
        {
            errors.AddRange(ValidateForm(form, names));
        }

        return errors;
    }

    public IReadOnlyList<string> ValidatePlugins(DeploymentManifest manifest)
    {
        var plugins = manifest.Plugins;
        if (plugins is null) return ["The manifest has no 'plugins' section."];

        var errors = new List<string>();
        if (!plugins.PackageId.StartsWith(_publisherPrefix + "_", StringComparison.Ordinal))
            errors.Add($"plugins.packageId must start with '{_publisherPrefix}_' ({plugins.PackageId}).");

        var assembly = ResolveInRepository(plugins.Assembly);
        if (assembly is null || !File.Exists(assembly))
            errors.Add($"Plug-in assembly not found, build SupportTicket.Plugins in Release first ({plugins.Assembly}).");

        var packageDirectory = ResolveInRepository(plugins.PackageDirectory);
        if (packageDirectory is null || !Directory.Exists(packageDirectory))
            errors.Add($"Plug-in package folder not found ({plugins.PackageDirectory}).");

        return errors;
    }

    /// <summary>Returns the absolute path, or null if it escapes the repository root.</summary>
    public string? ResolveInRepository(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath)) return null;

        var fullPath = Path.GetFullPath(Path.Combine(_repositoryRoot, relativePath));
        var rootWithSeparator = _repositoryRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase) ? fullPath : null;
    }

    private static IEnumerable<string> ValidateForm(FormEntry form, HashSet<string> webResourceNames)
    {
        var label = form.IsDiscovery ? $"Forms {form.Entity} ({form.FormType})" : $"Form {form.Entity}/{form.FormId}";

        if (form.IsDiscovery)
        {
            // No form id: the forms are discovered in Dataverse and chosen interactively.
            if (FormTypes.Parse(form.FormType) is null)
                yield return $"{label}: without a formId, formType must be 'main' or 'quickCreate' so the forms can be discovered.";
        }
        else if (!Guid.TryParse(form.FormId, out _))
        {
            yield return $"{label}: formId must be an explicit form GUID (or leave it empty and set formType to choose forms interactively).";
        }

        if (string.IsNullOrWhiteSpace(form.Entity))
            yield return $"{label}: entity is required.";

        if (!webResourceNames.Contains(form.Library))
            yield return $"{label}: library '{form.Library}' must also be listed under webResources.";

        if (form.Events.Count == 0)
            yield return $"{label}: no events declared.";

        foreach (var formEvent in form.Events)
        {
            if (!SupportedFormEvents.Contains(formEvent.Event))
                yield return $"{label}: event '{formEvent.Event}' is not supported (onload, onsave). Register OnChange at runtime.";

            if (!FunctionNamePattern().IsMatch(formEvent.Function))
                yield return $"{label}: '{formEvent.Function}' is not a valid function name.";
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_\\-./]+$")]
    private static partial Regex WebResourceNamePattern();

    [GeneratedRegex("^[A-Za-z_$][A-Za-z0-9_$]*(\\.[A-Za-z_$][A-Za-z0-9_$]*)*$")]
    private static partial Regex FunctionNamePattern();
}
