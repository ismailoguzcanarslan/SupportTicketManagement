using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Plugins;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Plugins;

public class PluginRegistrationPlannerTests
{
    private const string TypeName = "SupportTicket.Plugins.SupportTickets.SupportTicketValidationPlugin";
    private static readonly byte[] PackageBytes = [1, 2, 3];
    private static readonly Guid PluginTypeId = Guid.NewGuid();
    private static readonly SdkMessageInfo Update = new(Guid.NewGuid(), "Update", null);

    [Fact]
    public void NewPackage_PlansPackageAndStepCreation()
    {
        var plan = Plan([Step()], new RegistrationSnapshot { Messages = Messages(Update), Filters = Filter("Update") });

        Assert.Equal(PlannedAction.Create, plan.Package.Action);
        Assert.Equal(PlannedAction.Create, Assert.Single(plan.Steps).Action);
        Assert.True(plan.HasRegistrationChanges);
    }

    [Fact]
    public void MatchingRegistration_IsUnchanged()
    {
        var plan = Plan([Step()], Snapshot(ExistingFor(Step())));

        Assert.Equal(PlannedAction.Unchanged, plan.Package.Action);
        Assert.Equal(PlannedAction.Unchanged, Assert.Single(plan.Steps).Action);
        Assert.False(plan.HasRegistrationChanges);
    }

    [Fact]
    public void ApplyingThePlan_ThenPlanningAgain_IsIdempotent()
    {
        var declared = Step() with { Images = [new DeclaredImage(ImageType.PreImage, "PreImage", "ogz_ticket_title")] };
        var first = Plan([declared], new RegistrationSnapshot { Messages = Messages(Update), Filters = Filter("Update") });
        Assert.True(first.HasRegistrationChanges);

        // What Dataverse would contain after applying `first`.
        var deployed = ExistingFor(declared) with { Images = [new ExistingImage(Guid.NewGuid(), ImageType.PreImage, "PreImage", "ogz_ticket_title")] };
        var second = Plan([declared], Snapshot(deployed));

        Assert.False(second.HasRegistrationChanges);
        Assert.All(second.Steps, s => Assert.Equal(PlannedAction.Unchanged, s.Action));
    }

    [Fact]
    public void ChangedSettings_AreUpdatedInPlaceWithListedDifferences()
    {
        var existing = ExistingFor(Step()) with { Rank = 5, FilteringAttributes = "ogz_priority" };

        var step = Assert.Single(Plan([Step()], Snapshot(existing)).Steps);

        Assert.Equal(PlannedAction.Update, step.Action);
        Assert.Equal(existing.Id, step.ExistingStepId);
        Assert.Contains(step.Differences, d => d.StartsWith("rank 5 -> 1"));
        Assert.Contains(step.Differences, d => d.StartsWith("filtering attributes"));
    }

    [Fact]
    public void FilteringAttributeOrderAndCase_DoNotCountAsChange()
    {
        var declared = Step() with { FilteringAttributes = AttributeList.Normalize("ogz_ticket_title, OGZ_priority") };
        var existing = ExistingFor(declared) with { FilteringAttributes = "ogz_priority,ogz_ticket_title" };

        Assert.Equal(PlannedAction.Unchanged, Assert.Single(Plan([declared], Snapshot(existing)).Steps).Action);
    }

    [Fact]
    public void ChangedImageAttributes_UpdateTheImage()
    {
        var declared = Step() with { Images = [new DeclaredImage(ImageType.PreImage, "PreImage", "ogz_ticket_title")] };
        var existing = ExistingFor(declared) with { Images = [new ExistingImage(Guid.NewGuid(), ImageType.PreImage, "preimage", null)] };

        var step = Assert.Single(Plan([declared], Snapshot(existing)).Steps);

        Assert.Equal(PlannedAction.Update, Assert.Single(step.Images).Action);
    }

    [Fact]
    public void UnknownMessage_IsBlocked_NotCreated()
    {
        var declared = Step() with { Kind = StepKind.ActionStep, MessageName = "ogz_ProcessSupportTicket" };

        var step = Assert.Single(Plan([declared], Snapshot()).Steps);

        Assert.Equal(PlannedAction.Blocked, step.Action);
        Assert.Contains("does not exist", step.BlockedReason);
    }

    [Fact]
    public void MessageNotAvailableForTable_IsBlocked()
    {
        var plan = Plan([Step()], new RegistrationSnapshot { Messages = Messages(Update) });

        Assert.Contains("not available for table", Assert.Single(plan.Steps).BlockedReason);
    }

    [Fact]
    public void DuplicateExistingSteps_AreBlocked()
    {
        var existing = ExistingFor(Step());

        var plan = Plan([Step()], Snapshot(existing, existing with { Id = Guid.NewGuid() }));

        Assert.Equal(PlannedAction.Blocked, Assert.Single(plan.Steps).Action);
    }

    [Fact]
    public void UndeclaredRegistrations_AreReportedAsObsolete_NotDeleted()
    {
        var obsolete = ExistingFor(Step()) with { Id = Guid.NewGuid(), Stage = StepStage.PostOperation, Name = "old step" };

        var plan = Plan([Step()], Snapshot(ExistingFor(Step()), obsolete));

        Assert.Equal("old step", Assert.Single(plan.ObsoleteSteps).Name);
    }

    [Fact]
    public void ChangedStage_IsBlockedWhileTheOldStepIsStillRegistered()
    {
        var oldStep = ExistingFor(Step()) with { Name = "old PreValidation step" };
        var moved = Step() with { Stage = StepStage.PreOperation };

        var step = Assert.Single(Plan([moved], Snapshot(oldStep)).Steps);

        Assert.Equal(PlannedAction.Blocked, step.Action);
        Assert.Contains("'old PreValidation step' (PreValidation) is still registered", step.BlockedReason);
    }

    [Fact]
    public void AdditionalStage_IsCreated_WhenTheExistingStageIsStillDeclared()
    {
        var plan = Plan([Step(), Step() with { Stage = StepStage.PreOperation }], Snapshot(ExistingFor(Step())));

        Assert.Equal([PlannedAction.Unchanged, PlannedAction.Create], plan.Steps.Select(s => s.Action));
    }

    [Fact]
    public void SameClassRegisteredOutsideThePackage_IsBlocked()
    {
        var snapshot = Snapshot() with { ForeignSteps = [new ForeignStep(TypeName, "SupportTicket.Plugins (classic)", "manual step")] };

        var step = Assert.Single(Plan([Step()], snapshot).Steps);

        Assert.Equal(PlannedAction.Blocked, step.Action);
        Assert.Contains("registered outside the package", step.BlockedReason);
        Assert.Contains("manual step", step.BlockedReason);
    }

    [Fact]
    public void CustomApi_RespectsAllowedProcessingStepType()
    {
        var api = new SdkMessageInfo(Guid.NewGuid(), "ogz_ProcessSupportTicket",
            new CustomApiInfo(Guid.NewGuid(), "ogz_ProcessSupportTicket", null, 0, null));
        var declared = Step() with { Kind = StepKind.ActionStep, MessageName = api.Name, EntityLogicalName = null, Stage = StepStage.PostOperation, FilteringAttributes = null };

        var step = Assert.Single(Plan([declared], new RegistrationSnapshot { Messages = Messages(api) }).Steps);

        Assert.Equal(PlannedAction.Blocked, step.Action);
        Assert.Contains("does not allow custom processing steps", step.BlockedReason);
    }

    [Fact]
    public void CustomApiMainOperation_PlansLinkOnlyWhenUnlinked()
    {
        var declared = Step() with { Kind = StepKind.ActionStep, MessageName = "ogz_ProcessSupportTicket", EntityLogicalName = null, IsMainOperation = true, FilteringAttributes = null };
        CustomApiInfo Api(Guid? handler) => new(Guid.NewGuid(), "ogz_ProcessSupportTicket", handler, 0, null);
        RegistrationSnapshot With(CustomApiInfo api) => Snapshot() with
        {
            Messages = Messages(new SdkMessageInfo(Guid.NewGuid(), "ogz_ProcessSupportTicket", api)),
        };

        Assert.Equal(PlannedAction.Update, Assert.Single(Plan([declared], With(Api(null))).CustomApiLinks).Action);
        Assert.Equal(PlannedAction.Unchanged, Assert.Single(Plan([declared], With(Api(PluginTypeId))).CustomApiLinks).Action);
        Assert.Equal(PlannedAction.Blocked, Assert.Single(Plan([declared], With(Api(Guid.NewGuid()))).CustomApiLinks).Action);
        Assert.Empty(Plan([declared], With(Api(null))).Steps);
    }

    [Fact]
    public void PackageWithDifferentContent_IsUpdated()
    {
        var snapshot = Snapshot(ExistingFor(Step())) with { Package = new ExistingPackage(Guid.NewGuid(), "1.0.0", [9, 9]) };

        var plan = Plan([Step()], snapshot);

        Assert.Equal(PlannedAction.Update, plan.Package.Action);
        Assert.Equal("content changed", plan.Package.Reason);
        Assert.False(plan.IsBlocked);
    }

    [Fact]
    public void PackageVersionChange_IsBlocked_BecauseDataverseDoesNotAllowIt()
    {
        var plan = PluginRegistrationPlanner.Plan([Step()], Snapshot(ExistingFor(Step())), PackageBytes, "1.0.1");

        Assert.Equal(PlannedAction.Blocked, plan.Package.Action);
        Assert.True(plan.IsBlocked);
        Assert.Contains("back to 1.0.0", plan.Package.Reason);
    }

    [Fact]
    public void PackageContentThatCannotBeRead_IsUploadedAgain_NotSkipped()
    {
        var snapshot = Snapshot(ExistingFor(Step())) with { Package = new ExistingPackage(Guid.NewGuid(), "1.0.0", null) };

        Assert.Equal(PlannedAction.Update, Plan([Step()], snapshot).Package.Action);
    }

    [Fact]
    public void PackageThatRemovesPluginTypeWithSteps_IsBlocked()
    {
        var snapshot = Snapshot(ExistingFor(Step())) with { Package = new ExistingPackage(Guid.NewGuid(), "1.0.0", [9]) };

        var plan = PluginRegistrationPlanner.Plan([], snapshot, PackageBytes, "1.0.0", packagedPluginTypes: ["Other.Plugin"]);

        Assert.True(plan.IsBlocked);
        Assert.Contains(TypeName, plan.Package.Reason);
    }

    [Fact]
    public void PackageThatKeepsAllPluginTypes_IsNotBlocked()
    {
        var plan = PluginRegistrationPlanner.Plan([Step()], Snapshot(ExistingFor(Step())), PackageBytes, "1.0.0", packagedPluginTypes: [TypeName]);

        Assert.False(plan.IsBlocked);
    }

    [Fact]
    public void CustomApiMainHandler_MustMatchTheApiBinding()
    {
        var declared = Step() with { Kind = StepKind.ActionStep, MessageName = "ogz_ProcessSupportTicket", EntityLogicalName = null, IsMainOperation = true, FilteringAttributes = null };
        var boundApi = new CustomApiInfo(Guid.NewGuid(), "ogz_ProcessSupportTicket", null, 0, "ogz_support_ticket");
        var snapshot = Snapshot() with { Messages = Messages(new SdkMessageInfo(Guid.NewGuid(), boundApi.UniqueName, boundApi)) };

        var link = Assert.Single(Plan([declared], snapshot).CustomApiLinks);

        Assert.Equal(PlannedAction.Blocked, link.Action);
        Assert.Contains("is bound to 'ogz_support_ticket'", link.BlockedReason);

        var matching = declared with { EntityLogicalName = "ogz_support_ticket" };
        Assert.Equal(PlannedAction.Update, Assert.Single(Plan([matching], snapshot).CustomApiLinks).Action);
    }

    [Fact]
    public void StepOnCustomApi_IsBlocked_WhenThePluginIsAlreadyItsMainHandler()
    {
        var api = new CustomApiInfo(Guid.NewGuid(), "ogz_ProcessSupportTicket", PluginTypeId, 2, null);
        var declared = Step() with { Kind = StepKind.ActionStep, MessageName = api.UniqueName, EntityLogicalName = null, Stage = StepStage.PostOperation, FilteringAttributes = null };
        var snapshot = Snapshot() with { Messages = Messages(new SdkMessageInfo(Guid.NewGuid(), api.UniqueName, api)) };

        var step = Assert.Single(Plan([declared], snapshot).Steps);

        Assert.Equal(PlannedAction.Blocked, step.Action);
        Assert.Contains("already the main handler", step.BlockedReason);
    }

    private static PluginRegistrationPlan Plan(IReadOnlyList<DeclaredStep> steps, RegistrationSnapshot snapshot) =>
        PluginRegistrationPlanner.Plan(steps, snapshot, PackageBytes, "1.0.0");

    private static DeclaredStep Step() => new()
    {
        PluginTypeName = TypeName,
        Kind = StepKind.PluginStep,
        MessageName = "Update",
        EntityLogicalName = "ogz_support_ticket",
        Stage = StepStage.PreValidation,
        FilteringAttributes = "ogz_ticket_title",
        Name = DeclaredStep.DefaultName(TypeName, "Update", "ogz_support_ticket", StepStage.PreValidation),
    };

    private static ExistingStep ExistingFor(DeclaredStep step) => new()
    {
        Id = Guid.NewGuid(),
        PluginTypeId = PluginTypeId,
        MessageName = step.MessageName,
        EntityLogicalName = step.EntityLogicalName,
        Stage = step.Stage,
        Mode = step.Async ? 1 : 0,
        Rank = step.ExecutionOrder,
        FilteringAttributes = step.FilteringAttributes,
        Name = step.Name,
    };

    private static RegistrationSnapshot Snapshot(params ExistingStep[] steps) => new()
    {
        Package = new ExistingPackage(Guid.NewGuid(), "1.0.0", PackageBytes),
        PluginTypes = new Dictionary<string, Guid> { [TypeName] = PluginTypeId },
        Messages = Messages(Update),
        Filters = Filter("Update"),
        Steps = steps,
    };

    private static Dictionary<string, SdkMessageInfo> Messages(params SdkMessageInfo[] messages) =>
        messages.ToDictionary(m => m.Name, StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, Guid> Filter(string message) =>
        new() { [RegistrationSnapshot.FilterKey(message, "ogz_support_ticket")] = Guid.NewGuid() };
}
