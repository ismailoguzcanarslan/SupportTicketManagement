using SupportTicket.Entities;

namespace SupportTicket.BusinessLogic.SupportTickets;

/// <summary>Business rules for ogz_support_ticket. Pure validation: never writes to Dataverse.</summary>
public sealed class SupportTicketBusinessLogic
{
    /// <summary>Validates a ticket that is being created.</summary>
    public ValidationResult ValidateNewTicket(ogz_support_ticket ticket)
    {
        var result = new ValidationResult();

        if (string.IsNullOrWhiteSpace(ticket.ogz_ticket_title))
        {
            result.AddError("A support ticket must have a title.");
        }

        return result;
    }

    /// <summary>
    /// Validates the changed columns of a ticket that is being updated. Only columns present in
    /// <paramref name="changes"/> are checked, because an Update target contains changed columns only.
    /// </summary>
    public ValidationResult ValidateTicketChanges(ogz_support_ticket changes)
    {
        var result = new ValidationResult();

        if (changes.Contains(ogz_support_ticket.Fields.ogz_ticket_title)
            && string.IsNullOrWhiteSpace(changes.ogz_ticket_title))
        {
            result.AddError("The title of a support ticket cannot be cleared.");
        }

        return result;
    }
}
