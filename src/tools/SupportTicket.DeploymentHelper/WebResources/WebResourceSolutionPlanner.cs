using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Forms;

namespace SupportTicket.DeploymentHelper.WebResources;

/// <summary>Plans solution membership for Web Resources and configured forms. Pure.</summary>
public static class WebResourceSolutionPlanner
{
    public static string WebResourceLabel(string name) => $"Web Resource {name}";

    public static string FormLabel(FormPlanItem form) => $"Form {form.Entry.Entity}/{form.Entry.FormId}";

    /// <summary>Web Resources: new ones are created in the solution; existing ones are added if missing.</summary>
    public static IReadOnlyList<SolutionAssociation> PlanWebResources(
        IEnumerable<WebResourcePlanItem> plan,
        IReadOnlyDictionary<Guid, SolutionMember> members)
    {
        var associations = new List<SolutionAssociation>();
        foreach (var item in plan)
        {
            var label = WebResourceLabel(item.Local.Name);
            if (item.Action == PlannedAction.Create)
                associations.Add(SolutionAssociation.ForCreate(label, SolutionComponents.WebResource));
            else if (item.Action != PlannedAction.Blocked
                     && item.ExistingId is { } id
                     && SolutionAssociation.ForExisting(label, SolutionComponents.WebResource, id, members) is { } existing)
                associations.Add(existing);
        }

        return associations;
    }

    /// <summary>
    /// Only explicitly configured forms that can be patched (not blocked) join the solution, and only
    /// when neither a row of their own nor their table (with all subcomponents) already includes them.
    /// </summary>
    /// <param name="tables">The solution's table rows, keyed by table logical name.</param>
    public static IReadOnlyList<SolutionAssociation> PlanForms(
        IEnumerable<FormPlanItem> forms,
        IReadOnlyDictionary<Guid, SolutionMember> members,
        IReadOnlyDictionary<string, SolutionMember> tables)
    {
        var associations = new List<SolutionAssociation>();
        foreach (var form in forms.Where(f => f.Action != PlannedAction.Blocked && !FormMembership.IsIncludedByTable(f.Entry.Entity, tables)))
        {
            if (SolutionAssociation.ForExisting(FormLabel(form), SolutionComponents.SystemForm, Guid.Parse(form.Entry.FormId), members) is { } association)
                associations.Add(association);
        }

        return associations;
    }
}

/// <summary>Checks, from a fresh read of Dataverse, that a Web Resource deployment really landed. Pure.</summary>
public static class WebResourceDeploymentVerifier
{
    /// <param name="deployed">The local Web Resources that were deployed.</param>
    /// <param name="publishedAfter">The published Web Resources, read again after the deployment.</param>
    /// <param name="formsAfter">The deployed forms, planned again against their published FormXML.</param>
    /// <param name="tables">The solution's table rows, keyed by table logical name.</param>
    public static IReadOnlyList<string> Verify(
        IEnumerable<LocalWebResource> deployed,
        IReadOnlyDictionary<string, ExistingWebResource> publishedAfter,
        IEnumerable<FormPlanItem> formsAfter,
        IReadOnlyDictionary<Guid, SolutionMember> members,
        IReadOnlyDictionary<string, SolutionMember> tables,
        string solutionUniqueName)
    {
        var issues = new List<string>();

        foreach (var local in deployed)
        {
            var label = WebResourceSolutionPlanner.WebResourceLabel(local.Name);
            if (!publishedAfter.TryGetValue(local.Name, out var resource))
            {
                issues.Add($"{label}: not found.");
                continue;
            }

            if (!WebResourcePlanner.HasContent(resource, local.Content))
                issues.Add($"{label}: the published content is not the deployed build (not published).");
            if (!members.ContainsKey(resource.Id))
                issues.Add($"{label}: not in solution {solutionUniqueName}.");
        }

        foreach (var form in formsAfter)
        {
            var label = WebResourceSolutionPlanner.FormLabel(form);
            if (form.Action != PlannedAction.Unchanged)
                issues.Add($"{label}: library or handlers are missing from the published form ({(form.Changes.Count > 0 ? string.Join(", ", form.Changes) : form.Reason)}).");
            if (!FormMembership.IsMember(Guid.Parse(form.Entry.FormId), form.Entry.Entity, members, tables))
                issues.Add($"{label}: not in solution {solutionUniqueName}.");
        }

        return issues;
    }
}
