using System.Text.Json;

namespace SupportTicket.DeploymentHelper.Configuration;

/// <summary>What to deploy (deployment.manifest.json). File paths are relative to the repository root.</summary>
public sealed class DeploymentManifest
{
    public List<WebResourceEntry> WebResources { get; init; } = [];

    public List<FormEntry> Forms { get; init; } = [];

    public PluginsEntry? Plugins { get; init; }

    public static DeploymentManifest Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Deployment manifest not found: {path}", path);

        return JsonSerializer.Deserialize<DeploymentManifest>(File.ReadAllText(path), JsonDefaults.Options)
            ?? throw new InvalidDataException($"Deployment manifest is empty: {path}");
    }
}

public sealed class WebResourceEntry
{
    /// <summary>Web Resource unique name, e.g. "ogz_/forms/account.js".</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Built file, relative to the repository root.</summary>
    public string File { get; init; } = string.Empty;

    public string? DisplayName { get; init; }
}

public sealed class FormEntry
{
    /// <summary>Table logical name the form belongs to (validated against the form).</summary>
    public string Entity { get; init; } = string.Empty;

    /// <summary>
    /// Explicit systemform id (for automation). Leave empty to discover the forms of
    /// <see cref="Entity"/> and <see cref="FormType"/> and choose them interactively on each run.
    /// Forms are never looked up by name.
    /// </summary>
    public string FormId { get; init; } = string.Empty;

    /// <summary>Form type to discover when <see cref="FormId"/> is empty: "main" or "quickCreate".</summary>
    public string FormType { get; init; } = string.Empty;

    /// <summary>Web Resource name to add as a form library.</summary>
    public string Library { get; init; } = string.Empty;

    public List<FormEventEntry> Events { get; init; } = [];

    public bool IsDiscovery => string.IsNullOrWhiteSpace(FormId);

    /// <summary>The same registration, targeted at one concrete form.</summary>
    public FormEntry ForForm(Guid formId) => new()
    {
        Entity = Entity,
        FormId = formId.ToString(),
        FormType = FormType,
        Library = Library,
        Events = Events,
    };
}

/// <summary>systemform.type values the manifest can name.</summary>
public static class FormTypes
{
    public const int Main = 2;
    public const int QuickCreate = 7;

    public static int? Parse(string? name) => name?.Trim().ToLowerInvariant() switch
    {
        "main" => Main,
        "quickcreate" => QuickCreate,
        _ => null,
    };
}

public sealed class FormEventEntry
{
    /// <summary>"onload" or "onsave". OnChange handlers are registered at runtime by the form script.</summary>
    public string Event { get; init; } = string.Empty;

    /// <summary>Fully qualified handler, e.g. "Ogz.Forms.Account.onLoad".</summary>
    public string Function { get; init; } = string.Empty;

    public bool PassExecutionContext { get; init; } = true;
}

public sealed class PluginsEntry
{
    /// <summary>Plug-in package id (PackageId in SupportTicket.Plugins.csproj).</summary>
    public string PackageId { get; init; } = string.Empty;

    /// <summary>Built plug-in assembly whose registration attributes are read.</summary>
    public string Assembly { get; init; } = string.Empty;

    /// <summary>Folder containing the built {PackageId}.{Version}.nupkg.</summary>
    public string PackageDirectory { get; init; } = string.Empty;
}
