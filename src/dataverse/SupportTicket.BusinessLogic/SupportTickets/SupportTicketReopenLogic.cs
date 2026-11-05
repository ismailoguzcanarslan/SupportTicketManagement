using System;
using System.Globalization;
using Microsoft.Xrm.Sdk;
using SupportTicket.Entities;

namespace SupportTicket.BusinessLogic.SupportTickets;

/// <summary>
/// Business rules for reopening a support ticket. Builds what has to be written but never writes:
/// the caller (the ogz_ReopenSupportTicket Custom API) saves the results in one transaction.
/// </summary>
public sealed class SupportTicketReopenLogic
{
    public const string NoteSubject = "Ticket reopened";

    // annotation (Note) is not in the generated early-bound entities, so its columns are named here.
    private const string Annotation = "annotation";
    private const string AnnotationSubject = "subject";
    private const string AnnotationText = "notetext";
    private const string AnnotationRegarding = "objectid";

    /// <summary>Any inactive ticket (Resolved, Closed, Cancelled) can be reopened; an active one cannot.</summary>
    public ValidationResult CanReopen(ogz_support_ticket stored)
    {
        var result = new ValidationResult();

        if (stored.statecode != ogz_support_ticket_statecode.Inactive)
        {
            result.AddError("Only inactive tickets can be reopened.");
        }

        return result;
    }

    /// <summary>The columns to write: Active / New, and the description cleared.</summary>
    public ogz_support_ticket BuildReopenedTicket(Guid ticketId) =>
        new()
        {
            Id = ticketId,
            statecode = ogz_support_ticket_statecode.Active,
            statuscode = ogz_support_ticket_statuscode.New,
            ogz_description = null,
        };

    /// <summary>
    /// The timeline note that records who reopened the ticket and when. The time is written in UTC, because a
    /// plug-in does not know the reader's time zone; the note's own "Created On" shows the local time.
    /// </summary>
    public Entity BuildReopenNote(EntityReference ticket, DateTime reopenedOnUtc, string reopenedBy, string? reason)
    {
        if (ticket == null) throw new ArgumentNullException(nameof(ticket));

        var text = string.Format(
            CultureInfo.InvariantCulture,
            "Reopened on {0:yyyy-MM-dd HH:mm} UTC by {1}.",
            reopenedOnUtc,
            string.IsNullOrWhiteSpace(reopenedBy) ? "an unknown user" : reopenedBy);
        if (!string.IsNullOrWhiteSpace(reason)) text += " Reason: " + reason!.Trim();

        return new Entity(Annotation)
        {
            [AnnotationSubject] = NoteSubject,
            [AnnotationText] = text,
            [AnnotationRegarding] = new EntityReference(ticket.LogicalName, ticket.Id),
        };
    }
}
