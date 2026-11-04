using static SupportTicket.Tools.Dataverse.DataverseConnectionSettingsLoader;

namespace SupportTicket.Tools.Dataverse;

public sealed class DataverseConnectionSettings
{
    public string Url { get; init; } = string.Empty;
    public string AuthType { get; init; } = "Interactive";
    public string? ClientId { get; init; }

    // Read for future non-interactive auth types; the Interactive flow does not use them.
    public string? ClientSecret { get; init; }
    public string? TenantId { get; init; }

    public string? RedirectUri { get; init; }

    /// <summary>Environment URL reduced to scheme and host, e.g. "https://contoso.crm4.dynamics.com".</summary>
    public string EnvironmentUrl =>
        Uri.TryCreate(Url, UriKind.Absolute, out var uri) ? uri.GetLeftPart(UriPartial.Authority) : Url;

    /// <summary>Problems with the configured variables, each naming the variable to fix. Empty when valid.</summary>
    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Url))
            errors.Add($"{UrlVariable} is not set. Set it to your environment URL, e.g. https://<org>.crm4.dynamics.com.");
        else if (!Uri.TryCreate(Url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            errors.Add($"{UrlVariable} ('{Url}') is not a valid https URL.");

        if (string.IsNullOrWhiteSpace(ClientId))
            errors.Add($"{ClientIdVariable} is not set. Set it to the application (client) id used for sign-in.");
        else if (!Guid.TryParse(ClientId, out _))
            errors.Add($"{ClientIdVariable} ('{ClientId}') is not a GUID.");

        if (!string.Equals(AuthType, "Interactive", StringComparison.OrdinalIgnoreCase))
            errors.Add($"{AuthTypeVariable} '{AuthType}' is not supported. Only 'Interactive' is implemented; unset it or set it to Interactive.");

        if (RedirectUri is not null && !Uri.TryCreate(RedirectUri, UriKind.Absolute, out _))
            errors.Add($"{RedirectUriVariable} ('{RedirectUri}') is not a valid URL.");

        return errors;
    }
}
