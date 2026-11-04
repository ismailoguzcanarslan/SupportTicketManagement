using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.AzureFunctions;

/// <summary>
/// Menu option 3, reserved for Azure Functions deployment. Implement RunAsync and set IsAvailable
/// to true when that pipeline exists; the menu and the other targets need no changes.
/// </summary>
public sealed class AzureFunctionsDeploymentTarget : IDeploymentTarget
{
    public int MenuKey => 3;

    public string Title => "Deploy Azure Functions";

    public bool IsAvailable => false;

    public Task RunAsync(DeploymentRunOptions options, CancellationToken cancellationToken) =>
        throw new NotSupportedException("Azure Functions deployment is not implemented yet.");
}
