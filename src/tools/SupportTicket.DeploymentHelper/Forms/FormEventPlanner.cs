using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.Forms;

public sealed record SystemFormInfo(Guid Id, string Name, string Entity, string FormXml, bool IsCustomizable);

/// <param name="PublishPending">Unchanged, but the saved library and handlers are not published yet.</param>
public sealed record FormPlanItem(FormEntry Entry, PlannedAction Action, string Reason, IReadOnlyList<string> Changes, string? UpdatedXml, bool PublishPending = false)
{
    public string Label => $"form {Entry.Entity}/{Entry.FormId}";

    public string DisplayAction => PublishPending ? "Publish" : Action.ToString();
}

/// <summary>Validates a target form and computes the minimal FormXML change. Pure: no I/O.</summary>
public static class FormEventPlanner
{
    /// <summary>
    /// Plans against the latest saved FormXML (so unpublished edits are patched, not discarded) and
    /// marks a form whose saved XML is complete but whose published XML is not as PublishPending.
    /// </summary>
    public static FormPlanItem Plan(FormEntry entry, SystemFormInfo? latest, SystemFormInfo? published, Func<Guid> newId)
    {
        var plan = Plan(entry, latest, newId);
        return plan.Action == PlannedAction.Unchanged && Plan(entry, published, newId).Action != PlannedAction.Unchanged
            ? plan with { Reason = $"{plan.Reason}, but not published", PublishPending = true }
            : plan;
    }

    public static FormPlanItem Plan(FormEntry entry, SystemFormInfo? form, Func<Guid> newId)
    {
        if (form is null)
            return Blocked(entry, "form not found; check the formId");

        if (!string.Equals(form.Entity, entry.Entity, StringComparison.OrdinalIgnoreCase))
            return Blocked(entry, $"form '{form.Name}' belongs to '{form.Entity}', not '{entry.Entity}'");

        if (!form.IsCustomizable)
            return Blocked(entry, $"form '{form.Name}' is not customizable");

        var patch = FormXmlPatcher.Patch(form.FormXml, entry.Library, entry.Events, newId);
        if (!patch.Succeeded)
            return Blocked(entry, patch.Error!);

        return patch.HasChanges
            ? new FormPlanItem(entry, PlannedAction.Update, $"form '{form.Name}'", patch.Changes, patch.UpdatedXml)
            : new FormPlanItem(entry, PlannedAction.Unchanged, $"form '{form.Name}' already has the library and handlers", [], null);
    }

    private static FormPlanItem Blocked(FormEntry entry, string reason) =>
        new(entry, PlannedAction.Blocked, reason, [], null);
}
