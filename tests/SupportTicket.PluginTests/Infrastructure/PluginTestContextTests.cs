using System;
using Microsoft.Xrm.Sdk;
using NSubstitute;
using SupportTicket.Base.Registration;
using Xunit;

namespace SupportTicket.PluginTests.Infrastructure;

// Self-tests for the shared plug-in test infrastructure, so plug-in tests can rely on it.
public class PluginTestContextTests
{
    private const string TicketEntity = "ogz_support_ticket";

    [Fact]
    public void ServiceProvider_ResolvesPipelineServices()
    {
        var test = new PluginTestContext("Create", PluginStage.PreOperation, TicketEntity);

        var context = (IPluginExecutionContext)test.ServiceProvider.GetService(typeof(IPluginExecutionContext))!;
        var tracing = (ITracingService)test.ServiceProvider.GetService(typeof(ITracingService))!;
        var factory = (IOrganizationServiceFactory)test.ServiceProvider.GetService(typeof(IOrganizationServiceFactory))!;

        Assert.Equal("Create", context.MessageName);
        Assert.Equal((int)PluginStage.PreOperation, context.Stage);
        Assert.Equal(TicketEntity, context.PrimaryEntityName);
        Assert.Same(test.Tracing, tracing);
        Assert.Same(test.OrganizationService, factory.CreateOrganizationService(context.UserId));
        Assert.Equal(new Guid?[] { context.UserId }, test.ServiceFactory.RequestedUserIds);
    }

    [Fact]
    public void ServiceProvider_ReturnsNullForUnregisteredService()
    {
        var test = new PluginTestContext("Create", PluginStage.PreOperation, TicketEntity);

        Assert.Null(test.ServiceProvider.GetService(typeof(IServiceEndpointNotificationService)));
    }

    [Fact]
    public void WithTarget_SetsTargetAndPrimaryEntityId()
    {
        var target = new Entity(TicketEntity, Guid.NewGuid());

        var test = new PluginTestContext("Update", PluginStage.PreOperation, TicketEntity).WithTarget(target);

        Assert.Same(target, test.ExecutionContext.InputParameters["Target"]);
        Assert.Equal(target.Id, test.ExecutionContext.PrimaryEntityId);
    }

    [Fact]
    public void WithImages_ExposesPreAndPostEntityImages()
    {
        var preImage = new Entity(TicketEntity, Guid.NewGuid());
        var postImage = new Entity(TicketEntity, preImage.Id);

        var test = new PluginTestContext("Update", PluginStage.PostOperation, TicketEntity)
            .WithPreImage("PreImage", preImage)
            .WithPostImage("PostImage", postImage);

        Assert.Same(preImage, test.ExecutionContext.PreEntityImages["PreImage"]);
        Assert.Same(postImage, test.ExecutionContext.PostEntityImages["PostImage"]);
    }

    [Fact]
    public void Tracing_FormatsAndRecordsMessages()
    {
        var test = new PluginTestContext("Create", PluginStage.PreOperation, TicketEntity);

        test.Tracing.Trace("Ticket {0} has {1} notes", "T-001", 3);
        test.Tracing.Trace("No arguments {0}");

        Assert.Equal(new[] { "Ticket T-001 has 3 notes", "No arguments {0}" }, test.Tracing.Messages);
    }

    [Fact]
    public void OrganizationService_CanBeConfiguredAndVerified()
    {
        var test = new PluginTestContext("Update", PluginStage.PostOperation, TicketEntity);
        var category = new Entity("ogz_ticket_category", Guid.NewGuid());
        test.OrganizationService
            .Retrieve("ogz_ticket_category", category.Id, Arg.Any<Microsoft.Xrm.Sdk.Query.ColumnSet>())
            .Returns(category);

        var result = test.OrganizationService.Retrieve("ogz_ticket_category", category.Id, new Microsoft.Xrm.Sdk.Query.ColumnSet(true));

        Assert.Same(category, result);
        test.OrganizationService.Received(1).Retrieve("ogz_ticket_category", category.Id, Arg.Any<Microsoft.Xrm.Sdk.Query.ColumnSet>());
    }
}
