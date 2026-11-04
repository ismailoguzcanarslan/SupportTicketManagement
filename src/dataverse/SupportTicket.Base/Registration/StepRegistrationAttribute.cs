using System;

namespace SupportTicket.Base.Registration;

/// <summary>
/// Common registration metadata for an SDK message processing step. Read through reflection by
/// SupportTicket.DeploymentHelper; has no effect at runtime inside Dataverse.
/// </summary>
public abstract class StepRegistrationAttribute : Attribute
{
    protected StepRegistrationAttribute(
        string messageName,
        string? entityLogicalName,
        PluginStage stage,
        int executionOrder,
        bool async)
    {
        MessageName = messageName;
        EntityLogicalName = entityLogicalName;
        Stage = stage;
        ExecutionOrder = executionOrder;
        Async = async;
    }

    /// <summary>SDK message name, e.g. "Create" or a Custom API unique name.</summary>
    public string MessageName { get; }

    /// <summary>Primary table logical name, or null/"none" for an unbound message.</summary>
    public string? EntityLogicalName { get; }

    public PluginStage Stage { get; }

    /// <summary>Step rank (sdkmessageprocessingstep.rank). Lower runs first.</summary>
    public int ExecutionOrder { get; }

    /// <summary>True for asynchronous execution. Only allowed in the PostOperation stage.</summary>
    public bool Async { get; }

    /// <summary>Comma-separated attribute logical names that trigger the step (Update only). Empty = all attributes.</summary>
    public string? FilteringAttributes { get; set; }

    /// <summary>Optional step name. Defaults to "{PluginType}: {Message} of {Entity} ({Stage})".</summary>
    public string? Name { get; set; }

    public string? Description { get; set; }

    // No run-as user here on purpose: systemuser ids differ per environment, so a GUID in code would
    // point to the wrong user (or none) outside DEV. Steps run as the calling user.

    /// <summary>Pre-image alias. Setting it registers a pre-image for this step.</summary>
    public string? PreImageName { get; set; }

    /// <summary>Comma-separated attributes of the pre-image. Empty = all attributes.</summary>
    public string? PreImageAttributes { get; set; }

    /// <summary>Post-image alias. Setting it registers a post-image (PostOperation only).</summary>
    public string? PostImageName { get; set; }

    /// <summary>Comma-separated attributes of the post-image. Empty = all attributes.</summary>
    public string? PostImageAttributes { get; set; }

    /// <summary>Delete the system job when an asynchronous step succeeds.</summary>
    public bool DeleteAsyncOperationIfSuccessful { get; set; } = true;
}
