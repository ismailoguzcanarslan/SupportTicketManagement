using System;
using System.Reflection;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
using SupportTicket.Base.Registration;
using SupportTicket.Entities;
using SupportTicket.PluginTests.Infrastructure;
using SupportTicket.Plugins.SupportTickets;
using Xunit;

namespace SupportTicket.PluginTests.Plugins;

public class ReopenSupportTicketPluginTests
{
    private static readonly Guid TicketId = Guid.NewGuid();
    private static readonly DateTime ReopenedOn = new(2026, 11, 5, 11, 30, 0, DateTimeKind.Utc);

    /// <summary>A call of the Custom API on a ticket that Dataverse stores with <paramref name="state"/>.</summary>
    private static PluginTestContext Call(ogz_support_ticket_statecode? state, string? reason = null)
    {
        var test = new PluginTestContext(ReopenSupportTicketApi.Name, PluginStage.PostOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new EntityReference(ogz_support_ticket.EntityLogicalName, TicketId));
        test.ExecutionContext.OperationCreatedOn = ReopenedOn;
        if (reason is not null) test.ExecutionContext.InputParameters[ReopenSupportTicketApi.Parameters.Reason] = reason;

        var stored = new ogz_support_ticket { Id = TicketId };
        if (state is not null) stored.statecode = state;
        test.OrganizationService
            .Retrieve(ogz_support_ticket.EntityLogicalName, TicketId, Arg.Any<ColumnSet>())
            .Returns(stored);
        test.OrganizationService
            .Retrieve(SystemUser.EntityLogicalName, test.ExecutionContext.InitiatingUserId, Arg.Any<ColumnSet>())
            .Returns(new Entity(SystemUser.EntityLogicalName) { [SystemUser.Fields.FullName] = "Ali Yilmaz" });
        return test;
    }

    private static void Execute(PluginTestContext test) => new ReopenSupportTicketPlugin().Execute(test.ServiceProvider);

    [Fact]
    public void IsTheMainHandlerOfTheBoundCustomApi()
    {
        var step = typeof(ReopenSupportTicketPlugin).GetCustomAttribute<ActionStepAttribute>()!;

        Assert.Equal("ogz_ReopenSupportTicket", step.MessageName);
        Assert.Equal(ogz_support_ticket.EntityLogicalName, step.EntityLogicalName);
        Assert.True(step.IsMainOperation);
    }

    [Fact]
    public void ReopensAnInactiveTicketAsActiveNewWithoutDescription()
    {
        var test = Call(ogz_support_ticket_statecode.Inactive);

        Execute(test);

        test.OrganizationService.Received(1).Update(Arg.Is<Entity>(e =>
            e.LogicalName == ogz_support_ticket.EntityLogicalName
            && e.Id == TicketId
            && e.GetAttributeValue<OptionSetValue>("statecode").Value == (int)ogz_support_ticket_statecode.Active
            && e.GetAttributeValue<OptionSetValue>("statuscode").Value == (int)ogz_support_ticket_statuscode.New
            && e.Contains("ogz_description") && e["ogz_description"] == null));
    }

    [Fact]
    public void AddsATimelineNoteWithTimeAndUser()
    {
        var test = Call(ogz_support_ticket_statecode.Inactive);

        Execute(test);

        test.OrganizationService.Received(1).Create(Arg.Is<Entity>(e =>
            e.LogicalName == "annotation"
            && (string)e["notetext"] == "Reopened on 2026-11-05 11:30 UTC by Ali Yilmaz."
            && ((EntityReference)e["objectid"]).Id == TicketId));
    }

    [Fact]
    public void AddsTheOptionalReasonToTheNote()
    {
        var test = Call(ogz_support_ticket_statecode.Inactive, reason: "Customer called back");

        Execute(test);

        test.OrganizationService.Received(1).Create(Arg.Is<Entity>(e =>
            (string)e["notetext"] == "Reopened on 2026-11-05 11:30 UTC by Ali Yilmaz. Reason: Customer called back"));
    }

    [Fact]
    public void ReturnsWhenItWasReopened()
    {
        var test = Call(ogz_support_ticket_statecode.Inactive);

        Execute(test);

        Assert.Equal(ReopenedOn, test.ExecutionContext.OutputParameters[ReopenSupportTicketApi.Response.ReopenedOn]);
    }

    [Fact]
    public void ReadsTheStoredStateAndTheUserNameAsTheCallingUser()
    {
        var test = Call(ogz_support_ticket_statecode.Inactive);

        Execute(test);

        test.OrganizationService.Received(1).Retrieve(
            ogz_support_ticket.EntityLogicalName, TicketId, Arg.Is<ColumnSet>(c => c.Columns.Count == 1 && c.Columns[0] == "statecode"));
        test.OrganizationService.Received(1).Retrieve(
            SystemUser.EntityLogicalName, test.ExecutionContext.InitiatingUserId, Arg.Is<ColumnSet>(c => c.Columns[0] == "fullname"));
        Assert.Equal(new Guid?[] { test.ExecutionContext.UserId }, test.ServiceFactory.RequestedUserIds);
    }

    [Theory]
    [InlineData(ogz_support_ticket_statecode.Active)]
    [InlineData(null)]
    public void RejectsATicketThatIsNotInactiveAndWritesNothing(ogz_support_ticket_statecode? state)
    {
        var test = Call(state);

        var thrown = Assert.Throws<InvalidPluginExecutionException>(() => Execute(test));

        Assert.Equal("Only inactive tickets can be reopened.", thrown.Message);
        test.OrganizationService.DidNotReceive().Update(Arg.Any<Entity>());
        test.OrganizationService.DidNotReceive().Create(Arg.Any<Entity>());
    }

    [Fact]
    public void RejectsACallWithoutATicket()
    {
        var test = new PluginTestContext(ReopenSupportTicketApi.Name, PluginStage.PostOperation, ogz_support_ticket.EntityLogicalName);

        var thrown = Assert.Throws<InvalidPluginExecutionException>(() => Execute(test));

        Assert.Equal("ogz_ReopenSupportTicket must be called on a support ticket.", thrown.Message);
    }
}
