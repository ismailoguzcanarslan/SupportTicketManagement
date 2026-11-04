using System.Linq;
using Microsoft.Xrm.Sdk;
using SupportTicket.Base.Plugins;
using SupportTicket.Base.Registration;
using SupportTicket.BusinessLogic.SupportTickets;
using SupportTicket.Entities;

namespace SupportTicket.Plugins.SupportTickets;

/// <summary>
/// Rejects support tickets without a title. Validation only: it never changes the record.
/// </summary>
[PluginStep(
    MessageName.Create,
    ogz_support_ticket.EntityLogicalName,
    PluginStage.PreValidation,
    executionOrder: 1,
    async: false)]
[PluginStep(
    MessageName.Update,
    ogz_support_ticket.EntityLogicalName,
    PluginStage.PreValidation,
    executionOrder: 1,
    async: false,
    FilteringAttributes = ogz_support_ticket.Fields.ogz_ticket_title)]
public sealed class SupportTicketValidationPlugin : BasePlugin
{
    private static readonly SupportTicketBusinessLogic BusinessLogic = new();

    protected override void ExecutePlugin(PluginContext context)
    {
        var ticket = context.GetTarget<ogz_support_ticket>();

        var result = context.MessageName == MessageName.Create
            ? BusinessLogic.ValidateNewTicket(ticket)
            : BusinessLogic.ValidateTicketChanges(ticket);

        if (!result.IsValid)
        {
            throw new InvalidPluginExecutionException(string.Join(" ", result.Errors.ToArray()));
        }
    }
}
