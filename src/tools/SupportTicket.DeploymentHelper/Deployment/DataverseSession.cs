using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.Tools.Dataverse;

namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>
/// Lazily opened connection, shared by all deployment targets in one run. The connection comes from
/// the same OGZ_DATAVERSE_* variables as EntityGenerator, with the interactive Microsoft sign-in.
/// </summary>
/// <remarks>
/// DEV-only means: the settings must say DEV, there is no way to choose another environment, and
/// every write needs the user to confirm after seeing the connected environment. The tool cannot
/// prove that OGZ_DATAVERSE_URL is really DEV; that is the user's responsibility when setting it.
/// </remarks>
public sealed class DataverseSession : IDataverseSession, IDisposable
{
    private readonly DeploymentSettings _settings;
    private readonly DataverseConnectionService _connectionService;
    private readonly IUserPrompt _prompt;
    private readonly Func<DataverseConnectionSettings> _loadConnection;
    private ServiceClient? _client;
    private DataverseEnvironmentInfo? _environment;

    public DataverseSession(DeploymentSettings settings, DataverseConnectionService connectionService, IUserPrompt prompt)
        : this(settings, connectionService, prompt, DataverseConnectionSettingsLoader.Load)
    {
    }

    internal DataverseSession(
        DeploymentSettings settings,
        DataverseConnectionService connectionService,
        IUserPrompt prompt,
        Func<DataverseConnectionSettings> loadConnection)
    {
        _settings = settings;
        _connectionService = connectionService;
        _prompt = prompt;
        _loadConnection = loadConnection;
    }

    public string EnvironmentName => _settings.TargetEnvironment;

    public IOrganizationService Service => Client;

    public DataverseEnvironmentInfo Environment
    {
        get
        {
            _ = Client;
            return _environment!;
        }
    }

    /// <summary>"Org name (url)" of the connected environment, for confirmation prompts.</summary>
    public string Description => $"{Environment.FriendlyName} ({Environment.Url})";

    private ServiceClient Client => _client ??= Connect();

    public void Dispose() => _client?.Dispose();

    /// <summary>
    /// Validates the connection variables without connecting. Throws with every problem listed, so
    /// misconfiguration is reported before the sign-in window opens.
    /// </summary>
    public DataverseConnectionSettings LoadConnectionSettings()
    {
        _settings.EnsureTargetIsDev();
        var connection = _loadConnection();
        var errors = connection.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Dataverse connection is not configured:" + System.Environment.NewLine +
                string.Join(System.Environment.NewLine, errors.Select(e => "  - " + e)));
        }

        return connection;
    }

    private ServiceClient Connect()
    {
        var connection = LoadConnectionSettings();
        _prompt.WriteLine($"Connecting to {connection.EnvironmentUrl} (from {DataverseConnectionSettingsLoader.UrlVariable}) ...");

        var client = _connectionService.Connect(connection);
        try
        {
            _environment = DataverseEnvironmentInfo.Read(client);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        foreach (var line in DescribeConnection(EnvironmentName, _environment)) _prompt.WriteLine(line);
        return client;
    }

    internal static IEnumerable<string> DescribeConnection(string environmentName, DataverseEnvironmentInfo environment)
    {
        yield return string.Empty;
        yield return $"  Environment:  {environmentName}";
        yield return $"  Dataverse:    {environment.FriendlyName} ({environment.UniqueName})";
        yield return $"  URL:          {environment.Url}";
        yield return $"  Organization: {environment.OrganizationId}";
        yield return $"  Check that this is your {environmentName} environment: the tool deploys to whatever {DataverseConnectionSettingsLoader.UrlVariable} points to.";
        yield return string.Empty;
    }
}
