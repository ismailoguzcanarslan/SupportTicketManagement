using System;
using Microsoft.Xrm.Sdk;
using NSubstitute;
using SupportTicket.Base.Registration;
using SupportTicket.Entities;
using SupportTicket.PluginTests.Infrastructure;
using SupportTicket.Plugins.SupportTickets;
using Xunit;

namespace SupportTicket.PluginTests.Plugins;

// The plug-in declares no images, so none are set up here.
public class SupportTicketValidationPluginTests
{
    [Fact]
    public void Create_WithoutTitle_IsRejectedWithTheBusinessMessage()
    {
        var test = Context(MessageName.Create, new Entity(ogz_support_ticket.EntityLogicalName));

        var thrown = Assert.Throws<InvalidPluginExecutionException>(
            () => new SupportTicketValidationPlugin().Execute(test.ServiceProvider));

        Assert.Equal("A support ticket must have a title.", thrown.Message);
    }

    [Fact]
    public void Create_WithBlankTitle_IsRejected()
    {
        var test = Context(MessageName.Create, Ticket(title: "   "));

        Assert.Throws<InvalidPluginExecutionException>(() => new SupportTicketValidationPlugin().Execute(test.ServiceProvider));
    }

    [Fact]
    public void Create_WithTitle_PassesWithoutCallingDataverse()
    {
        var test = Context(MessageName.Create, Ticket(title: "Printer offline"));

        new SupportTicketValidationPlugin().Execute(test.ServiceProvider);

        NoDataverseCalls(test);
    }

    [Fact]
    public void Create_DoesNotModifyTheTarget()
    {
        var target = Ticket(title: "Printer offline");
        var test = Context(MessageName.Create, target);

        new SupportTicketValidationPlugin().Execute(test.ServiceProvider);

        Assert.Single(target.Attributes);
        Assert.Equal("Printer offline", target[ogz_support_ticket.Fields.ogz_ticket_title]);
    }

    [Fact]
    public void Update_ClearingTitle_IsRejectedWithTheUpdateMessage()
    {
        var test = Context(MessageName.Update, Ticket(Guid.NewGuid(), title: null));

        var thrown = Assert.Throws<InvalidPluginExecutionException>(
            () => new SupportTicketValidationPlugin().Execute(test.ServiceProvider));

        Assert.Equal("The title of a support ticket cannot be cleared.", thrown.Message);
    }

    [Fact]
    public void Update_WithNewTitle_Passes()
    {
        var test = Context(MessageName.Update, Ticket(Guid.NewGuid(), title: "Renamed"));

        new SupportTicketValidationPlugin().Execute(test.ServiceProvider);

        NoDataverseCalls(test);
    }

    [Fact]
    public void Update_PartialTargetWithoutTitle_Passes()
    {
        var target = new Entity(ogz_support_ticket.EntityLogicalName, Guid.NewGuid())
        {
            [ogz_support_ticket.Fields.ogz_priority] = new OptionSetValue((int)ogz_support_ticket_ogz_priority.High),
        };
        var test = Context(MessageName.Update, target);

        new SupportTicketValidationPlugin().Execute(test.ServiceProvider);

        NoDataverseCalls(test);
    }

    [Fact]
    public void UnexpectedTargetType_FailsLoudlyInsteadOfSkippingValidation()
    {
        var test = new PluginTestContext(MessageName.Delete, PluginStage.PreValidation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new EntityReference(ogz_support_ticket.EntityLogicalName, Guid.NewGuid()));

        Assert.Throws<InvalidPluginExecutionException>(() => new SupportTicketValidationPlugin().Execute(test.ServiceProvider));
    }

    private static Entity Ticket(Guid? id = null, string? title = null) =>
        new(ogz_support_ticket.EntityLogicalName, id ?? Guid.Empty) { [ogz_support_ticket.Fields.ogz_ticket_title] = title };

    private static void NoDataverseCalls(PluginTestContext test)
    {
        Assert.Empty(test.ServiceFactory.RequestedUserIds);
        Assert.Empty(test.OrganizationService.ReceivedCalls());
    }

    private static PluginTestContext Context(string message, Entity target) =>
        new PluginTestContext(message, PluginStage.PreValidation, ogz_support_ticket.EntityLogicalName).WithTarget(target);
}
