using System;
using Microsoft.Xrm.Sdk;
using SupportTicket.Base.Plugins;
using SupportTicket.Base.Registration;
using SupportTicket.Entities;
using SupportTicket.PluginTests.Infrastructure;
using Xunit;

namespace SupportTicket.PluginTests.Base;

public class PluginContextTests
{
    [Fact]
    public void GetTarget_ReturnsEarlyBoundEntity()
    {
        var target = new Entity(ogz_support_ticket.EntityLogicalName, Guid.NewGuid())
        {
            [ogz_support_ticket.Fields.ogz_ticket_title] = "Printer offline",
        };
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(target);

        var ticket = new PluginContext(test.ServiceProvider).GetTarget<ogz_support_ticket>();

        Assert.Equal("Printer offline", ticket.ogz_ticket_title);
        Assert.Equal(target.Id, ticket.Id);
    }

    [Fact]
    public void GetTarget_ThrowsWhenTargetIsNotAnEntity()
    {
        var test = new PluginTestContext(MessageName.Delete, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new EntityReference(ogz_support_ticket.EntityLogicalName, Guid.NewGuid()));
        var context = new PluginContext(test.ServiceProvider);

        Assert.Throws<InvalidPluginExecutionException>(() => context.GetTarget<ogz_support_ticket>());
        Assert.NotNull(context.TargetReference);
        Assert.Null(context.Target);
    }

    [Fact]
    public void GetImages_ReturnTypedImagesOrNull()
    {
        var id = Guid.NewGuid();
        var test = new PluginTestContext(MessageName.Update, PluginStage.PostOperation, ogz_support_ticket.EntityLogicalName)
            .WithPreImage("PreImage", new Entity(ogz_support_ticket.EntityLogicalName, id) { ["ogz_ticket_title"] = "Old" })
            .WithPostImage("PostImage", new Entity(ogz_support_ticket.EntityLogicalName, id) { ["ogz_ticket_title"] = "New" });
        var context = new PluginContext(test.ServiceProvider);

        Assert.Equal("Old", context.GetPreImage<ogz_support_ticket>("PreImage")!.ogz_ticket_title);
        Assert.Equal("New", context.GetPostImage<ogz_support_ticket>("PostImage")!.ogz_ticket_title);
        Assert.Null(context.GetPreImage<ogz_support_ticket>("Missing"));
    }

    [Fact]
    public void OrganizationServices_AreCreatedLazilyForUserAndSystem()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName);
        var context = new PluginContext(test.ServiceProvider);

        Assert.Empty(test.ServiceFactory.RequestedUserIds);

        _ = context.OrganizationService;
        _ = context.OrganizationService;
        _ = context.SystemOrganizationService;

        Assert.Equal(new Guid?[] { test.ExecutionContext.UserId, null }, test.ServiceFactory.RequestedUserIds);
    }
}
