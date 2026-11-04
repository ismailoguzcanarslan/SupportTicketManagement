using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Plugins;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Plugins;

public class PluginSolutionTests
{
    private const int PackageType = 10090;
    private const string Solution = "OguzLabsSupportTicketManagement";
    private const string TypeName = "SupportTicket.Plugins.SupportTickets.SupportTicketValidationPlugin";
    private static readonly Guid TypeId = Guid.NewGuid();
    private static readonly Guid PackageId = Guid.NewGuid();

    [Fact]
    public void NewPackageAndSteps_AreCreatedInTheSolution()
    {
        var plan = Plan(new PackagePlan(PlannedAction.Create, "1.0.0", "new package"), StepPlanFor(PlannedAction.Create, null));

        var associations = PluginSolutionPlanner.Plan(plan, Members(), PackageType);

        Assert.Equal(
            ["Plugin Package", "Plugin Step - Update / ogz_support_ticket (PreValidation, SupportTicketValidationPlugin)"],
            associations.Select(a => a.Label));
        Assert.All(associations, a => Assert.Equal(AssociationAction.CreatedInSolution, a.Action));
        Assert.Equal([PackageType, SolutionComponents.SdkMessageProcessingStep], associations.Select(a => a.ComponentType));
    }

    [Fact]
    public void ExistingComponentsOutsideTheSolution_AreAdded()
    {
        var stepId = Guid.NewGuid();
        var plan = Plan(new PackagePlan(PlannedAction.Unchanged, "1.0.0", "content identical", PackageId), StepPlanFor(PlannedAction.Unchanged, stepId));

        var associations = PluginSolutionPlanner.Plan(plan, Members(), PackageType);

        Assert.Equal([PackageId, stepId], associations.Select(a => a.ObjectId!.Value));
        Assert.All(associations, a => Assert.Equal(AssociationAction.Add, a.Action));
    }

    [Fact]
    public void ComponentsAlreadyInTheSolution_AreNotAddedAgain()
    {
        var stepId = Guid.NewGuid();
        var plan = Plan(new PackagePlan(PlannedAction.Update, "1.0.0", "content changed", PackageId), StepPlanFor(PlannedAction.Update, stepId));

        Assert.Empty(PluginSolutionPlanner.Plan(plan, Members(PackageId, stepId), PackageType));
    }

    [Fact]
    public void BlockedPlansAndSteps_GetNoSolutionChanges()
    {
        var blockedPackage = Plan(new PackagePlan(PlannedAction.Blocked, "1.0.1", "version change", PackageId), StepPlanFor(PlannedAction.Create, null));
        var blockedStep = Plan(new PackagePlan(PlannedAction.Unchanged, "1.0.0", "same", PackageId), StepPlanFor(PlannedAction.Blocked, null));

        Assert.Empty(PluginSolutionPlanner.Plan(blockedPackage, Members(), PackageType));
        Assert.Empty(PluginSolutionPlanner.Plan(blockedStep, Members(PackageId), PackageType));
    }

    [Fact]
    public void Verification_PassesWhenEverythingIsRegisteredAndInTheSolution()
    {
        var (after, stepId, imageId) = Deployed();

        Assert.Empty(PluginDeploymentVerifier.Verify([StepPlanFor(PlannedAction.Create, null, withImage: true)], after, Members(PackageId, stepId, imageId), Solution));
    }

    [Fact]
    public void Verification_AcceptsImagesIncludedThroughTheirStep()
    {
        var (after, stepId, _) = Deployed();
        var members = new Dictionary<Guid, SolutionMember>
        {
            [PackageId] = new(PackageId, PackageType, 0),
            [stepId] = new(stepId, SolutionComponents.SdkMessageProcessingStep, RootComponentBehavior: 0),
        };

        Assert.Empty(PluginDeploymentVerifier.Verify([StepPlanFor(PlannedAction.Create, null, withImage: true)], after, members, Solution));
    }

    [Fact]
    public void Verification_ReportsEveryMissingPieceExplicitly()
    {
        var (after, stepId, _) = Deployed();
        var members = new Dictionary<Guid, SolutionMember> { [stepId] = new(stepId, SolutionComponents.SdkMessageProcessingStep, RootComponentBehavior: 1) };

        var issues = PluginDeploymentVerifier.Verify([StepPlanFor(PlannedAction.Create, null, withImage: true)], after, members, Solution);

        Assert.Contains($"The plug-in package is not in solution {Solution}.", issues);
        Assert.Contains(issues, i => i.Contains("image PreImage is not in solution"));
    }

    [Fact]
    public void Verification_ReportsMissingPackageAndSteps()
    {
        var plan = StepPlanFor(PlannedAction.Create, null);

        Assert.Equal(["The plug-in package was not found after deployment."],
            PluginDeploymentVerifier.Verify([plan], new RegistrationSnapshot(), Members(), Solution));

        var noSteps = new RegistrationSnapshot
        {
            Package = new ExistingPackage(PackageId, "1.0.0", null),
            PluginTypes = new Dictionary<string, Guid> { [TypeName] = TypeId },
        };
        Assert.Contains(PluginDeploymentVerifier.Verify([plan], noSteps, Members(PackageId), Solution), i => i.EndsWith("step not found."));
    }

    private static (RegistrationSnapshot After, Guid StepId, Guid ImageId) Deployed()
    {
        var stepId = Guid.NewGuid();
        var imageId = Guid.NewGuid();
        var after = new RegistrationSnapshot
        {
            Package = new ExistingPackage(PackageId, "1.0.0", null),
            PluginTypes = new Dictionary<string, Guid> { [TypeName] = TypeId },
            Steps =
            [
                new ExistingStep
                {
                    Id = stepId,
                    PluginTypeId = TypeId,
                    MessageName = "Update",
                    EntityLogicalName = "ogz_support_ticket",
                    Stage = StepStage.PreValidation,
                    Images = [new ExistingImage(imageId, ImageType.PreImage, "PreImage", "ogz_ticket_title")],
                },
            ],
        };
        return (after, stepId, imageId);
    }

    private static PluginRegistrationPlan Plan(PackagePlan package, params StepPlan[] steps) => new(package, steps, [], [], []);

    private static StepPlan StepPlanFor(PlannedAction action, Guid? existingId, bool withImage = false)
    {
        var declared = new DeclaredStep
        {
            PluginTypeName = TypeName,
            Kind = StepKind.PluginStep,
            MessageName = "Update",
            EntityLogicalName = "ogz_support_ticket",
            Stage = StepStage.PreValidation,
            Name = "step",
            Images = withImage ? [new DeclaredImage(ImageType.PreImage, "PreImage", "ogz_ticket_title")] : [],
        };
        return new StepPlan(declared, action, existingId, [], [], action == PlannedAction.Blocked ? "blocked" : null);
    }

    private static Dictionary<Guid, SolutionMember> Members(params Guid[] ids) =>
        ids.ToDictionary(id => id, id => new SolutionMember(id, 0, 0));
}
