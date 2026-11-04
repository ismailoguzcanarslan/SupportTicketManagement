
using Microsoft.PowerPlatform.Dataverse.Client;

namespace SupportTicket.Tools.Dataverse;

public sealed class DataverseConnectionService
{
    /// <summary>Signs in interactively (Microsoft login) and returns a ready connection.</summary>
    public ServiceClient Connect(DataverseConnectionSettings settings)
    {
        var errors = settings.Validate();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Dataverse connection is not configured:" + Environment.NewLine +
                string.Join(Environment.NewLine, errors.Select(e => "  - " + e)));
        }

        var connectionString =
            $"AuthType=OAuth;" +
            $"Url={settings.EnvironmentUrl};" +
            $"AppId={settings.ClientId};" +
            $"RedirectUri={settings.RedirectUri ?? "http://localhost"};" +
            $"LoginPrompt=Auto;" +
            $"RequireNewInstance=true";

        var client = new ServiceClient(connectionString);

        if (!client.IsReady)
        {
            var error = client.LastError;
            client.Dispose();

            throw new InvalidOperationException(
                $"Dataverse connection failed: {error}");
        }

        return client;
    }
}
