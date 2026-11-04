using Microsoft.Xrm.Sdk;

namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>
/// The connection the deployment targets and repositories work with. The real implementation is
/// <see cref="DataverseSession"/>; tests use an in-memory organization service behind this boundary.
/// </summary>
public interface IDataverseSession
{
    /// <summary>Target environment name from the settings (always DEV).</summary>
    string EnvironmentName { get; }

    /// <summary>"Org name (url)" of the connected environment, for confirmation prompts.</summary>
    string Description { get; }

    IOrganizationService Service { get; }
}
