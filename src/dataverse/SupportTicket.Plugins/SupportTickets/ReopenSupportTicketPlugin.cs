using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SupportTicket.Base.Plugins;
using SupportTicket.Base.Registration;
using SupportTicket.BusinessLogic.SupportTickets;
using SupportTicket.Entities;

namespace SupportTicket.Plugins.SupportTickets;

/// <summary>
/// Main handler of the ogz_ReopenSupportTicket Custom API (bound to ogz_support_ticket): reopens an inactive
/// ticket as Active / New, clears its description and adds a timeline note saying when and by whom.
/// </summary>
/// <remarks>
/// The rules live in <see cref="SupportTicketReopenLogic"/>; this class only reads, calls them and writes.
/// The update and the note run in the Custom API's transaction, so either both are saved or neither.
/// Writes run as the calling user, so the user needs write access to the ticket and create access to notes.
/// </remarks>
[ActionStep(
    ReopenSupportTicketApi.Name,
    ogz_support_ticket.EntityLogicalName,
    PluginStage.PostOperation,
    IsMainOperation = true)]
public sealed class ReopenSupportTicketPlugin : BasePlugin
{
    private static readonly SupportTicketReopenLogic Logic = new();

    protected override void ExecutePlugin(PluginContext context)
    {
        var ticket = context.TargetReference
            ?? throw new InvalidPluginExecutionException($"{ReopenSupportTicketApi.Name} must be called on a support ticket.");
        var service = context.OrganizationService;

        var stored = service
            .Retrieve(ticket.LogicalName, ticket.Id, new ColumnSet(ogz_support_ticket.Fields.statecode))
            .ToEntity<ogz_support_ticket>();
        var result = Logic.CanReopen(stored);
        if (!result.IsValid) throw new InvalidPluginExecutionException(string.Join(" ", result.Errors));

        var reopenedOn = context.ExecutionContext.OperationCreatedOn;
        var reopenedBy = service
            .Retrieve(SystemUser.EntityLogicalName, context.ExecutionContext.InitiatingUserId, new ColumnSet(SystemUser.Fields.FullName))
            .GetAttributeValue<string>(SystemUser.Fields.FullName);
        var reason = context.GetInput<string>(ReopenSupportTicketApi.Parameters.Reason);

        service.Update(Logic.BuildReopenedTicket(ticket.Id));
        service.Create(Logic.BuildReopenNote(ticket, reopenedOn, reopenedBy, reason));

        context.SetOutput(ReopenSupportTicketApi.Response.ReopenedOn, reopenedOn);
        context.Trace("{0}: ticket {1} reopened by {2}.", nameof(ReopenSupportTicketPlugin), ticket.Id, reopenedBy);
    }
}
