using System;

namespace SupportTicket.Base.Registration;

/// <summary>Declares a step for a standard SDK message (Create, Update, RetrieveMultiple, ...).</summary>
/// <example>
/// [PluginStep(MessageName.Update, "ogz_support_ticket", PluginStage.PreOperation,
///     executionOrder: 1, async: false, FilteringAttributes = "ogz_ticket_title")]
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class PluginStepAttribute : StepRegistrationAttribute
{
    public PluginStepAttribute(
        string messageName,
        string? entityLogicalName,
        PluginStage stage,
        int executionOrder = 1,
        bool async = false)
        : base(messageName, entityLogicalName, stage, executionOrder, async)
    {
    }
}
