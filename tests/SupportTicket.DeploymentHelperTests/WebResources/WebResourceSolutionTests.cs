using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Forms;
using SupportTicket.DeploymentHelper.WebResources;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.WebResources;

public class WebResourceSolutionTests
{
    private const string Solution = "OguzLabsSupportTicketManagement";
    private const string Name = "ogz_/forms/account.js";
    private static readonly byte[] Content = [1];
    private static readonly Guid FormId = Guid.NewGuid();

    [Fact]
    public void NewWebResource_IsCreatedInTheSolution_ExistingOneIsAddedOnce()
    {
        var existingId = Guid.NewGuid();
        var plan = new[]
        {
            Item(Name, PlannedAction.Create, null),
            Item("ogz_/forms/contact.js", PlannedAction.Unchanged, existingId),
            Item("ogz_/forms/lead.js", PlannedAction.Blocked, Guid.NewGuid()),
        };

        var associations = WebResourceSolutionPlanner.PlanWebResources(plan, Members());

        Assert.Equal([AssociationAction.CreatedInSolution, AssociationAction.Add], associations.Select(a => a.Action));
        Assert.Equal(existingId, associations[1].ObjectId);
        Assert.Empty(WebResourceSolutionPlanner.PlanWebResources(plan.Skip(1), Members(existingId)));
    }

    [Fact]
    public void OnlyConfiguredFormsThatAreNotBlocked_JoinTheSolution()
    {
        var forms = new[] { Form(PlannedAction.Unchanged), Form(PlannedAction.Blocked, Guid.NewGuid()) };

        var association = Assert.Single(WebResourceSolutionPlanner.PlanForms(forms, Members(), NoTables()));

        Assert.Equal(SolutionComponents.SystemForm, association.ComponentType);
        Assert.Equal(FormId, association.ObjectId);
        Assert.Empty(WebResourceSolutionPlanner.PlanForms(forms, Members(FormId), NoTables()));
    }

    // Regression: the table was created in the solution, so its forms are in it without rows of their
    // own. Adding the form again adds no row, and verification used to report it as missing.
    [Fact]
    public void FormOfATableInTheSolutionWithAllSubcomponents_IsNotAddedAgain_AndVerifies()
    {
        var tables = Tables(rootComponentBehavior: 0);

        Assert.Empty(WebResourceSolutionPlanner.PlanForms([Form(PlannedAction.Unchanged)], Members(), tables));
        Assert.True(FormMembership.IsMember(FormId, "account", Members(), tables));
        Assert.Empty(WebResourceDeploymentVerifier.Verify([], After(Guid.NewGuid()), [Form(PlannedAction.Unchanged)], Members(), tables, Solution));
    }

    [Theory]
    [InlineData(1)] // table without its subcomponents
    [InlineData(2)] // table as a shell
    public void FormOfATableWithoutItsSubcomponents_IsAddedExplicitly_AndIsMissingUntilThen(int rootComponentBehavior)
    {
        var tables = Tables(rootComponentBehavior);

        Assert.Equal(FormId, Assert.Single(WebResourceSolutionPlanner.PlanForms([Form(PlannedAction.Unchanged)], Members(), tables)).ObjectId);
        Assert.Contains($"Form account/{FormId}: not in solution {Solution}.",
            WebResourceDeploymentVerifier.Verify([], After(Guid.NewGuid()), [Form(PlannedAction.Unchanged)], Members(), tables, Solution));
        Assert.Empty(WebResourceSolutionPlanner.PlanForms([Form(PlannedAction.Unchanged)], Members(FormId), tables));
    }

    [Fact]
    public void TableMembershipCoversOnlyThatTablesForms()
    {
        Assert.False(FormMembership.IsMember(FormId, "contact", Members(), Tables(0)));
    }

    [Fact]
    public void Verification_PassesWhenWebResourceAndFormAreDeployedPublishedAndInTheSolution()
    {
        var id = Guid.NewGuid();

        Assert.Empty(WebResourceDeploymentVerifier.Verify([Local(Name)], After(id), [Form(PlannedAction.Unchanged)], Members(id, FormId), NoTables(), Solution));
    }

    [Fact]
    public void Verification_ReportsMissingResourceMembershipAndFormHandlers()
    {
        var id = Guid.NewGuid();

        var issues = WebResourceDeploymentVerifier.Verify([Local(Name), Local("ogz_/forms/missing.js")], After(id), [Form(PlannedAction.Update)], Members(), NoTables(), Solution);

        Assert.Contains($"Web Resource {Name}: not in solution {Solution}.", issues);
        Assert.Contains("Web Resource ogz_/forms/missing.js: not found.", issues);
        Assert.Contains(issues, i => i.Contains("library or handlers are missing from the published form"));
        Assert.Contains(issues, i => i.Contains($"Form account/{FormId}: not in solution"));
    }

    [Fact]
    public void Verification_ReportsAWebResourceWhosePublishedContentIsNotTheBuild()
    {
        var id = Guid.NewGuid();
        var stale = new Dictionary<string, ExistingWebResource> { [Name] = new(id, Name, 3, Convert.ToBase64String([9]), false) };

        var issue = Assert.Single(WebResourceDeploymentVerifier.Verify([Local(Name)], stale, [], Members(id), NoTables(), Solution));

        Assert.Equal($"Web Resource {Name}: the published content is not the deployed build (not published).", issue);
    }

    private static Dictionary<string, ExistingWebResource> After(Guid id) =>
        new() { [Name] = new ExistingWebResource(id, Name, 3, Convert.ToBase64String(Content), false) };

    private static LocalWebResource Local(string name) => new(name, null, 3, Content);

    private static WebResourcePlanItem Item(string name, PlannedAction action, Guid? id) =>
        new(Local(name), action, id, "test");

    private static FormPlanItem Form(PlannedAction action, Guid? formId = null) =>
        new(new FormEntry { Entity = "account", FormId = (formId ?? FormId).ToString(), Library = Name }, action, "test",
            action == PlannedAction.Update ? ["add onload handler Ogz.Forms.Account.onLoad"] : [], null);

    private static Dictionary<Guid, SolutionMember> Members(params Guid[] ids) =>
        ids.ToDictionary(id => id, id => new SolutionMember(id, 0, 0));

    private static Dictionary<string, SolutionMember> NoTables() => new(StringComparer.OrdinalIgnoreCase);

    private static Dictionary<string, SolutionMember> Tables(int rootComponentBehavior) =>
        new(StringComparer.OrdinalIgnoreCase) { ["Account"] = new SolutionMember(Guid.NewGuid(), SolutionComponents.Entity, rootComponentBehavior) };
}
