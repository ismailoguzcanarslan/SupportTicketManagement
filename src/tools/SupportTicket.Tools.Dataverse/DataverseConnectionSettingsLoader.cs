namespace SupportTicket.Tools.Dataverse;

/// <summary>
/// Reads the Dataverse connection from OGZ_DATAVERSE_* environment variables, shared by every tool.
/// Nothing is read from files, so no URL, id or secret is ever committed.
/// </summary>
public static class DataverseConnectionSettingsLoader
{
    public const string UrlVariable = "OGZ_DATAVERSE_URL";
    public const string AuthTypeVariable = "OGZ_DATAVERSE_AUTH_TYPE";
    public const string ClientIdVariable = "OGZ_DATAVERSE_CLIENT_ID";
    public const string ClientSecretVariable = "OGZ_DATAVERSE_CLIENT_SECRET";
    public const string TenantIdVariable = "OGZ_DATAVERSE_TENANT_ID";
    public const string RedirectUriVariable = "OGZ_DATAVERSE_REDIRECT_URI";

    public static DataverseConnectionSettings Load() => Load(Environment.GetEnvironmentVariable);

    /// <param name="getVariable">Reads one environment variable; injectable for tests.</param>
    public static DataverseConnectionSettings Load(Func<string, string?> getVariable)
    {
        string? Read(string name) => getVariable(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

        return new DataverseConnectionSettings
        {
            Url = Read(UrlVariable) ?? string.Empty,
            AuthType = Read(AuthTypeVariable) ?? "Interactive",
            ClientId = Read(ClientIdVariable),
            ClientSecret = Read(ClientSecretVariable),
            TenantId = Read(TenantIdVariable),
            RedirectUri = Read(RedirectUriVariable),
        };
    }
}
