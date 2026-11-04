using System.Text.Json;

namespace SupportTicket.DeploymentHelper.Configuration;

/// <summary>Non-secret deployment settings (deployment.settings.json). The connection comes from OGZ_DATAVERSE_* variables.</summary>
public sealed class DeploymentSettings
{
    /// <summary>The only environment this tool deploys to.</summary>
    public const string AllowedEnvironment = "DEV";

    public string TargetEnvironment { get; init; } = AllowedEnvironment;

    /// <summary>Unmanaged solution every deployed component is added to.</summary>
    public string SolutionUniqueName { get; init; } = string.Empty;

    public string PublisherPrefix { get; init; } = string.Empty;

    /// <summary>Manifest path, relative to the settings file.</summary>
    public string Manifest { get; init; } = "deployment.manifest.json";

    public static DeploymentSettings Load(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException($"Deployment settings not found: {path}", path);

        return JsonSerializer.Deserialize<DeploymentSettings>(File.ReadAllText(path), JsonDefaults.Options)
            ?? throw new InvalidDataException($"Deployment settings are empty: {path}");
    }

    /// <summary>
    /// Refuses any target but DEV. There is no environment selection: the tool always connects to
    /// OGZ_DATAVERSE_URL and treats it as DEV, so this only stops the settings from naming another target.
    /// </summary>
    public void EnsureTargetIsDev()
    {
        if (!string.Equals(TargetEnvironment, AllowedEnvironment, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Target environment '{TargetEnvironment}' is not allowed. This tool only deploys to {AllowedEnvironment}.");
        }
    }
}

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}
