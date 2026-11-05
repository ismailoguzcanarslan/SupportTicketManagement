using System;
using Microsoft.Xrm.Sdk;
using SupportTicket.BusinessLogic.SupportTickets;
using SupportTicket.Entities;
using Xunit;

namespace SupportTicket.PluginTests.BusinessLogic;

public class SupportTicketReopenLogicTests
{
    private static readonly Guid Id = Guid.NewGuid();
    private static readonly DateTime ReopenedOn = new(2026, 11, 5, 11, 30, 0, DateTimeKind.Utc);
    private readonly SupportTicketReopenLogic _logic = new();

    [Theory]
    [InlineData(ogz_support_ticket_statuscode.Resolved)]
    [InlineData(ogz_support_ticket_statuscode.Closed)]
    [InlineData(ogz_support_ticket_statuscode.Cancelled)]
    public void CanReopen_AcceptsEveryInactiveTicket(ogz_support_ticket_statuscode status)
    {
        var stored = new ogz_support_ticket { statecode = ogz_support_ticket_statecode.Inactive, statuscode = status };

        Assert.True(_logic.CanReopen(stored).IsValid);
    }

    [Fact]
    public void CanReopen_RejectsAnActiveTicket()
    {
        var result = _logic.CanReopen(new ogz_support_ticket { statecode = ogz_support_ticket_statecode.Active });

        Assert.Equal(["Only inactive tickets can be reopened."], result.Errors);
    }

    [Fact]
    public void CanReopen_RejectsATicketWithoutState()
    {
        Assert.False(_logic.CanReopen(new ogz_support_ticket()).IsValid);
    }

    [Fact]
    public void BuildReopenedTicket_SetsActiveNewAndClearsTheDescription()
    {
        var ticket = _logic.BuildReopenedTicket(Id);

        Assert.Equal(Id, ticket.Id);
        Assert.Equal(ogz_support_ticket_statecode.Active, ticket.statecode);
        Assert.Equal(ogz_support_ticket_statuscode.New, ticket.statuscode);
        Assert.True(ticket.Contains(ogz_support_ticket.Fields.ogz_description));
        Assert.Null(ticket.ogz_description);
    }

    [Fact]
    public void BuildReopenNote_RecordsWhenAndByWhomOnTheTicket()
    {
        var note = _logic.BuildReopenNote(new EntityReference(ogz_support_ticket.EntityLogicalName, Id), ReopenedOn, "Ali Yilmaz", reason: null);

        Assert.Equal("annotation", note.LogicalName);
        Assert.Equal(SupportTicketReopenLogic.NoteSubject, note["subject"]);
        Assert.Equal("Reopened on 2026-11-05 11:30 UTC by Ali Yilmaz.", note["notetext"]);
        var regarding = (EntityReference)note["objectid"];
        Assert.Equal(ogz_support_ticket.EntityLogicalName, regarding.LogicalName);
        Assert.Equal(Id, regarding.Id);
    }

    [Fact]
    public void BuildReopenNote_AddsTheReasonWhenGiven()
    {
        var note = _logic.BuildReopenNote(new EntityReference(ogz_support_ticket.EntityLogicalName, Id), ReopenedOn, "Ali Yilmaz", "  Customer called back  ");

        Assert.Equal("Reopened on 2026-11-05 11:30 UTC by Ali Yilmaz. Reason: Customer called back", note["notetext"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void BuildReopenNote_NamesAnUnknownUserWhenTheNameIsMissing(string? name)
    {
        var note = _logic.BuildReopenNote(new EntityReference(ogz_support_ticket.EntityLogicalName, Id), ReopenedOn, name!, reason: " ");

        Assert.Equal("Reopened on 2026-11-05 11:30 UTC by an unknown user.", note["notetext"]);
    }

    [Fact]
    public void BuildReopenNote_RequiresTheTicket()
    {
        Assert.Throws<ArgumentNullException>(() => _logic.BuildReopenNote(null!, ReopenedOn, "Ali Yilmaz", null));
    }
}
