using SupportTicket.BusinessLogic.SupportTickets;
using SupportTicket.Entities;
using Xunit;

namespace SupportTicket.PluginTests.BusinessLogic;

// Business rules are tested without any plug-in infrastructure or Dataverse service.
// The only rule today: a ticket must have a non-blank title (ogz_ticket_title).
public class SupportTicketBusinessLogicTests
{
    private readonly SupportTicketBusinessLogic _logic = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void ValidateNewTicket_RejectsMissingOrBlankTitle(string? title)
    {
        var result = _logic.ValidateNewTicket(new ogz_support_ticket { ogz_ticket_title = title });

        Assert.False(result.IsValid);
        Assert.Equal(["A support ticket must have a title."], result.Errors);
    }

    [Fact]
    public void ValidateNewTicket_RejectsTicketWithoutTitleColumn()
    {
        Assert.False(_logic.ValidateNewTicket(new ogz_support_ticket()).IsValid);
    }

    [Theory]
    [InlineData("Printer offline")]
    [InlineData("x")]
    [InlineData("  padded title  ")]
    public void ValidateNewTicket_AcceptsAnyNonBlankTitle(string title)
    {
        var result = _logic.ValidateNewTicket(new ogz_support_ticket { ogz_ticket_title = title });

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateNewTicket_IgnoresOtherColumns()
    {
        var ticket = new ogz_support_ticket
        {
            ogz_ticket_title = "Printer offline",
            ogz_priority = ogz_support_ticket_ogz_priority.Critical,
        };

        Assert.True(_logic.ValidateNewTicket(ticket).IsValid);
    }

    [Fact]
    public void ValidateTicketChanges_AcceptsPartialTargetWithoutTitle()
    {
        // An Update target only contains changed columns; a missing title means "unchanged".
        var changes = new ogz_support_ticket { ogz_priority = ogz_support_ticket_ogz_priority.High };

        Assert.True(_logic.ValidateTicketChanges(changes).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void ValidateTicketChanges_RejectsClearedTitle(string? title)
    {
        var result = _logic.ValidateTicketChanges(new ogz_support_ticket { ogz_ticket_title = title });

        Assert.Equal(["The title of a support ticket cannot be cleared."], result.Errors);
    }

    [Fact]
    public void ValidateTicketChanges_AcceptsNewTitle()
    {
        Assert.True(_logic.ValidateTicketChanges(new ogz_support_ticket { ogz_ticket_title = "Renamed" }).IsValid);
    }
}
