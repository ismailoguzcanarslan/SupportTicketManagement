namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>
/// One entry in the Deployment Helper menu. Add a new deployment target by implementing this
/// interface and registering it in Program.cs; the menu is built from the registered targets.
/// </summary>
public interface IDeploymentTarget
{
    /// <summary>Number shown in the menu. 0 is reserved for Exit.</summary>
    int MenuKey { get; }

    string Title { get; }

    /// <summary>False for placeholders: shown as "(Coming Soon)" and not runnable.</summary>
    bool IsAvailable { get; }

    /// <summary>Validates, plans, shows the plan and applies it only after explicit confirmation.</summary>
    Task RunAsync(DeploymentRunOptions options, CancellationToken cancellationToken);
}

public sealed record DeploymentRunOptions(bool DryRunOnly);
