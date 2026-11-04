using Microsoft.Crm.Sdk.Messages;
using Microsoft.PowerPlatform.Dataverse.Client;

namespace SupportTicket.Tools.Dataverse;

/// <summary>Identity of the environment a connection actually reached, as reported by Dataverse.</summary>
public sealed record DataverseEnvironmentInfo(string FriendlyName, string UniqueName, string Url, Guid OrganizationId, Guid UserId)
{
    /// <summary>Asks the server (WhoAmI) rather than trusting the configured URL.</summary>
    public static DataverseEnvironmentInfo Read(ServiceClient client)
    {
        var whoAmI = (WhoAmIResponse)client.Execute(new WhoAmIRequest());

        return new DataverseEnvironmentInfo(
            client.ConnectedOrgFriendlyName ?? string.Empty,
            client.ConnectedOrgUniqueName ?? string.Empty,
            client.ConnectedOrgUriActual?.GetLeftPart(UriPartial.Authority) ?? string.Empty,
            whoAmI.OrganizationId,
            whoAmI.UserId);
    }

    public override string ToString() => $"{FriendlyName} ({UniqueName}) {Url}, organization {OrganizationId}";
}
