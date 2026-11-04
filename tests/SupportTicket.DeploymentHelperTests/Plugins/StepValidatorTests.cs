using SupportTicket.DeploymentHelper.Plugins;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Plugins;

public class StepValidatorTests
{
    [Fact]
    public void ValidStep_HasNoErrors()
    {
        Assert.Empty(StepValidator.Validate([Step("Update", StepStage.PreOperation) with { FilteringAttributes = "ogz_ticket_title" }]));
    }

    [Theory]
    [InlineData(StepStage.PreValidation)]
    [InlineData(StepStage.PreOperation)]
    public void AsyncOutsidePostOperation_IsRejected(int stage)
    {
        Assert.Contains(StepValidator.Validate([Step("Update", stage) with { Async = true }]), e => e.Contains("asynchronous"));
    }

    [Fact]
    public void FilteringAttributesOnCreate_AreRejected()
    {
        Assert.Contains(StepValidator.Validate([Step("Create", StepStage.PreOperation) with { FilteringAttributes = "a" }]),
            e => e.Contains("only supported on Update"));
    }

    [Fact]
    public void ImageRules_AreEnforced()
    {
        var preOnCreate = Step("Create", StepStage.PostOperation) with { Images = [new DeclaredImage(ImageType.PreImage, "Pre", null)] };
        var postInPreOp = Step("Update", StepStage.PreOperation) with { Images = [new DeclaredImage(ImageType.PostImage, "Post", null)] };
        var onRetrieve = Step("RetrieveMultiple", StepStage.PreOperation) with { Images = [new DeclaredImage(ImageType.PreImage, "Pre", null)] };

        var errors = StepValidator.Validate([preOnCreate, postInPreOp, onRetrieve]);

        Assert.Contains(errors, e => e.Contains("Create has no pre-image"));
        Assert.Contains(errors, e => e.Contains("post-images are only available in PostOperation"));
        Assert.Contains(errors, e => e.Contains("not supported for RetrieveMultiple"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("none")]
    public void PluginStepWithoutTable_IsRejected(string? entity)
    {
        var step = Step("Create", StepStage.PreOperation) with { EntityLogicalName = AttributeList.NormalizeEntity(entity) };

        Assert.Contains(StepValidator.Validate([step]), e => e.Contains("would run for every table"));
    }

    [Fact]
    public void UnboundActionStep_IsAllowed()
    {
        var step = Step("ogz_ProcessSupportTicket", StepStage.PostOperation) with { Kind = StepKind.ActionStep, EntityLogicalName = null };

        Assert.Empty(StepValidator.Validate([step]));
    }

    [Fact]
    public void SameStepDeclaredTwice_IsRejected()
    {
        Assert.Contains(StepValidator.Validate([Step("Update", StepStage.PreOperation), Step("Update", StepStage.PreOperation)]),
            e => e.Contains("more than once"));
    }

    [Fact]
    public void MainOperation_IsOnlyForActionSteps()
    {
        Assert.Contains(StepValidator.Validate([Step("Update", StepStage.PostOperation) with { IsMainOperation = true }]),
            e => e.Contains("only valid on ActionStep"));
    }

    [Fact]
    public void MainHandlerAndStepOfSamePluginOnSameMessage_AreRejected()
    {
        var main = Step("ogz_ProcessSupportTicket", StepStage.PostOperation) with { Kind = StepKind.ActionStep, EntityLogicalName = null, IsMainOperation = true };
        var step = main with { IsMainOperation = false };

        Assert.Contains(StepValidator.Validate([main, step]), e => e.Contains("both as main handler and as a step"));
    }

    [Fact]
    public void TwoMainHandlersForOneCustomApi_AreRejected()
    {
        var first = Step("ogz_ProcessSupportTicket", StepStage.PostOperation) with { Kind = StepKind.ActionStep, EntityLogicalName = null, IsMainOperation = true };
        var second = first with { PluginTypeName = "Fixture.Other" };

        Assert.Contains(StepValidator.Validate([first, second]), e => e.Contains("more than one declared main handler"));
    }

    [Fact]
    public void ImageMessageProperty_FollowsTheMessage()
    {
        Assert.Equal("Id", StepValidator.ImageMessageProperty("Create"));
        Assert.Equal("Target", StepValidator.ImageMessageProperty("Update"));
        Assert.Equal("EntityMoniker", StepValidator.ImageMessageProperty("SetStateDynamicEntity"));
    }

    private static DeclaredStep Step(string message, int stage) => new()
    {
        PluginTypeName = "Fixture.Plugin",
        Kind = StepKind.PluginStep,
        MessageName = message,
        EntityLogicalName = "ogz_support_ticket",
        Stage = stage,
        Name = "fixture",
    };
}
