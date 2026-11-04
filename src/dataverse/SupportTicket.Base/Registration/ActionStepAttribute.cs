using System;

namespace SupportTicket.Base.Registration;

/// <summary>
/// Declares a step for a Custom API or Custom Action message.
/// The message itself must already exist in Dataverse: this attribute never creates it.
/// For a Custom API, the deployment tool plans linking the plug-in type as the API's main
/// handler; for a Custom Action it plans a regular step.
/// </summary>
/// <example>
/// [ActionStep("ogz_ProcessSupportTicket", "ogz_support_ticket", PluginStage.PostOperation, executionOrder: 1)]
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ActionStepAttribute : StepRegistrationAttribute
{
    public ActionStepAttribute(
        string messageName,
        string? entityLogicalName,
        PluginStage stage,
        int executionOrder = 1,
        bool async = false)
        : base(messageName, entityLogicalName, stage, executionOrder, async)
    {
    }

    /// <summary>
    /// Custom API only: make this plug-in type the API's main operation handler (customapi.plugintypeid)
    /// instead of registering a step. Stage, images and filtering do not apply. Linking changes the
    /// Custom API definition, so the deployment tool asks for a separate approval.
    /// </summary>
    public bool IsMainOperation { get; set; }
}
