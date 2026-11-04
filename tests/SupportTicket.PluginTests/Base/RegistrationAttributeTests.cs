using System.Linq;
using System.Reflection;
using SupportTicket.Base.Registration;
using SupportTicket.Plugins.SupportTickets;
using Xunit;

namespace SupportTicket.PluginTests.Base;

public class RegistrationAttributeTests
{
    [Fact]
    public void ValidationPlugin_DeclaresExactlyItsCreateAndFilteredUpdateSteps()
    {
        var steps = typeof(SupportTicketValidationPlugin).GetCustomAttributes<PluginStepAttribute>().ToList();

        Assert.Equal(2, steps.Count);
        Assert.Empty(typeof(SupportTicketValidationPlugin).GetCustomAttributes<ActionStepAttribute>());

        foreach (var step in steps)
        {
            Assert.Equal("ogz_support_ticket", step.EntityLogicalName);
            Assert.Equal(PluginStage.PreValidation, step.Stage);
            Assert.Equal(1, step.ExecutionOrder);
            Assert.False(step.Async);
            Assert.Null(step.PreImageName);
            Assert.Null(step.PostImageName);
            Assert.Null(step.Name);
        }

        Assert.Null(steps.Single(s => s.MessageName == MessageName.Create).FilteringAttributes);
        Assert.Equal("ogz_ticket_title", steps.Single(s => s.MessageName == MessageName.Update).FilteringAttributes);
    }

    [Fact]
    public void PluginStep_Defaults_AreSynchronousOrderOneWithoutImages()
    {
        var step = typeof(DefaultsFixture).GetCustomAttribute<PluginStepAttribute>()!;

        Assert.Equal(1, step.ExecutionOrder);
        Assert.False(step.Async);
        Assert.True(step.DeleteAsyncOperationIfSuccessful);
        Assert.Null(step.FilteringAttributes);
        Assert.Null(step.PreImageName);
    }

    [Fact]
    public void ActionStep_CarriesCustomMessageAndImages()
    {
        var step = typeof(ActionFixture).GetCustomAttribute<ActionStepAttribute>()!;

        Assert.Equal("ogz_ProcessSupportTicket", step.MessageName);
        Assert.Equal(PluginStage.PostOperation, step.Stage);
        Assert.Equal(2, step.ExecutionOrder);
        Assert.Equal("PreImage", step.PreImageName);
        Assert.Equal("ogz_ticket_title,ogz_priority", step.PreImageAttributes);
        Assert.False(step.IsMainOperation);
    }

    [Fact]
    public void ActionStep_CanDeclareACustomApiMainHandler()
    {
        var step = typeof(MainHandlerFixture).GetCustomAttribute<ActionStepAttribute>()!;

        Assert.True(step.IsMainOperation);
        Assert.Null(step.EntityLogicalName);
    }

    [Fact]
    public void Attributes_AllowSeveralStepsPerClass_AndAreNotInherited()
    {
        var usage = typeof(PluginStepAttribute).GetCustomAttribute<System.AttributeUsageAttribute>()!;

        Assert.True(usage.AllowMultiple);
        Assert.False(usage.Inherited);
    }

    [PluginStep(MessageName.Create, "ogz_support_ticket", PluginStage.PreOperation)]
    private sealed class DefaultsFixture
    {
    }

    [ActionStep("ogz_ProcessSupportTicket", "ogz_support_ticket", PluginStage.PostOperation, executionOrder: 2,
        PreImageName = "PreImage", PreImageAttributes = "ogz_ticket_title,ogz_priority")]
    private sealed class ActionFixture
    {
    }

    [ActionStep("ogz_ProcessSupportTicket", null, PluginStage.PostOperation, IsMainOperation = true)]
    private sealed class MainHandlerFixture
    {
    }
}
