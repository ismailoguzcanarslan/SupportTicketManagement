using System;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using NSubstitute;
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
    public void GetChange_CombinesTheTargetWithThePreImageOfTheDefaultAlias()
    {
        var id = Guid.NewGuid();
        var test = new PluginTestContext(MessageName.Update, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new Entity(ogz_support_ticket.EntityLogicalName, id) { [ogz_support_ticket.Fields.ogz_ticket_title] = "New" })
            .WithPreImage(ImageNames.PreImage, new Entity(ogz_support_ticket.EntityLogicalName, id)
            {
                [ogz_support_ticket.Fields.ogz_ticket_title] = "Old",
                [ogz_support_ticket.Fields.ogz_description] = "Printer offline",
            });

        var change = new PluginContext(test.ServiceProvider).GetChange<ogz_support_ticket>();

        Assert.True(change.HasChangedFrom(ogz_support_ticket.Fields.ogz_ticket_title, "Old"));
        Assert.Equal("New", change.Current.ogz_ticket_title);
        Assert.Equal("Printer offline", change.Current.ogz_description);
        Assert.Equal(id, change.Current.Id);
    }

    [Fact]
    public void GetChange_ReadsAnotherAliasWhenGiven()
    {
        var id = Guid.NewGuid();
        var test = new PluginTestContext(MessageName.Update, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new Entity(ogz_support_ticket.EntityLogicalName, id))
            .WithPreImage("Before", new Entity(ogz_support_ticket.EntityLogicalName, id) { [ogz_support_ticket.Fields.ogz_ticket_title] = "Old" });
        var context = new PluginContext(test.ServiceProvider);

        Assert.Equal("Old", context.GetChange<ogz_support_ticket>("Before").Previous!.ogz_ticket_title);
        Assert.Null(context.GetChange<ogz_support_ticket>().Previous);
    }

    [Fact]
    public void GetChange_EnsureColumnsReadsTheStoredRecordAsTheUser()
    {
        var id = Guid.NewGuid();
        var test = new PluginTestContext(MessageName.Update, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new Entity(ogz_support_ticket.EntityLogicalName, id));
        test.OrganizationService
            .Retrieve(ogz_support_ticket.EntityLogicalName, id, Arg.Any<ColumnSet>())
            .Returns(new Entity(ogz_support_ticket.EntityLogicalName, id) { [ogz_support_ticket.Fields.ogz_description] = "Stored" });

        var change = new PluginContext(test.ServiceProvider).GetChange<ogz_support_ticket>()
            .EnsureColumns(ogz_support_ticket.Fields.ogz_description);

        Assert.Equal("Stored", change.Current.ogz_description);
        test.OrganizationService.Received(1).Retrieve(
            ogz_support_ticket.EntityLogicalName,
            id,
            Arg.Is<ColumnSet>(columns => columns.Columns.Count == 1 && columns.Columns[0] == ogz_support_ticket.Fields.ogz_description));
        Assert.Equal(new Guid?[] { test.ExecutionContext.UserId }, test.ServiceFactory.RequestedUserIds);
    }

    [Theory]
    [InlineData(PluginStage.PreValidation, false)]
    [InlineData(PluginStage.PreOperation, false)]
    [InlineData(PluginStage.PostOperation, true)]
    public void GetChange_OnCreate_ReadsTheRecordOnlyOnceItIsSaved(PluginStage stage, bool reads)
    {
        var id = Guid.NewGuid();
        var test = new PluginTestContext(MessageName.Create, stage, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new Entity(ogz_support_ticket.EntityLogicalName, id));

        new PluginContext(test.ServiceProvider).GetChange<ogz_support_ticket>().EnsureColumns(ogz_support_ticket.Fields.ogz_ticket_number);

        test.OrganizationService.Received(reads ? 1 : 0).Retrieve(Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<ColumnSet>());
    }

    [Fact]
    public void GetChange_OnDelete_UsesTheTargetReferenceAndThePreImage()
    {
        var id = Guid.NewGuid();
        var test = new PluginTestContext(MessageName.Delete, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new EntityReference(ogz_support_ticket.EntityLogicalName, id))
            .WithPreImage(ImageNames.PreImage, new Entity(ogz_support_ticket.EntityLogicalName, id) { [ogz_support_ticket.Fields.ogz_ticket_title] = "Old" });

        var change = new PluginContext(test.ServiceProvider).GetChange<ogz_support_ticket>();

        Assert.Null(change.Target);
        Assert.Equal(id, change.Current.Id);
        Assert.Equal("Old", change.Current.ogz_ticket_title);
    }

    [Fact]
    public void GetChange_UsesThePrimaryEntityIdWhenTheTargetHasNoId()
    {
        var id = Guid.NewGuid();
        var test = new PluginTestContext(MessageName.Update, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName)
            .WithTarget(new Entity(ogz_support_ticket.EntityLogicalName));
        test.ExecutionContext.PrimaryEntityId = id;

        Assert.Equal(id, new PluginContext(test.ServiceProvider).GetChange<ogz_support_ticket>().Current.Id);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public void IsDepthAbove_ComparesWithTheExecutionDepth(int depth, bool above)
    {
        var test = new PluginTestContext(MessageName.Update, PluginStage.PreOperation, ogz_support_ticket.EntityLogicalName);
        test.ExecutionContext.Depth = depth;

        Assert.Equal(above, new PluginContext(test.ServiceProvider).IsDepthAbove(1));
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
