using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.WebResources;

/// <param name="DisplayName">From the manifest; null when the manifest does not set one.</param>
public sealed record LocalWebResource(string Name, string? DisplayName, int Type, byte[] Content);

public sealed record ExistingWebResource(Guid Id, string Name, int Type, string? ContentBase64, bool IsManaged, string? DisplayName = null);

public sealed record WebResourcePlanItem(
    LocalWebResource Local,
    PlannedAction Action,
    Guid? ExistingId,
    string Reason,
    bool ContentChanged = false,
    bool DisplayNameChanged = false,
    bool PublishPending = false)
{
    /// <summary>Plan label: an unchanged item whose saved content is not published yet only needs publishing.</summary>
    public string DisplayAction => PublishPending ? "Publish" : Action.ToString();

    /// <summary>Dataverse stores Web Resource content as base64 of the raw file bytes.</summary>
    public string ContentBase64 => Convert.ToBase64String(Local.Content);

    /// <summary>Display name used when creating: the manifest's, or the Web Resource name.</summary>
    public string CreateDisplayName => Local.DisplayName ?? Local.Name;
}

/// <summary>
/// Decides create/update/unchanged per Web Resource by comparing file bytes and, when the manifest
/// sets one, the display name. A display name that the manifest omits is never overwritten. Pure: no I/O.
/// </summary>
public static class WebResourcePlanner
{
    /// <param name="existing">The latest saved Web Resources (what an update replaces).</param>
    /// <param name="published">
    /// The published copies. When given, an item whose saved content already matches but whose
    /// published content does not (an earlier publish failed or never ran) is marked PublishPending.
    /// </param>
    public static IReadOnlyList<WebResourcePlanItem> Plan(
        IEnumerable<LocalWebResource> local,
        IReadOnlyDictionary<string, ExistingWebResource> existing,
        IReadOnlyDictionary<string, ExistingWebResource>? published = null)
    {
        var plan = new List<WebResourcePlanItem>();

        foreach (var resource in local)
        {
            if (!existing.TryGetValue(resource.Name, out var current))
            {
                plan.Add(new WebResourcePlanItem(resource, PlannedAction.Create, null, "new"));
                continue;
            }

            if (current.IsManaged)
            {
                plan.Add(new WebResourcePlanItem(resource, PlannedAction.Blocked, current.Id, "exists in a managed solution"));
                continue;
            }

            if (current.Type != resource.Type)
            {
                plan.Add(new WebResourcePlanItem(resource, PlannedAction.Blocked, current.Id,
                    $"exists with a different type ({current.Type}, expected {resource.Type})"));
                continue;
            }

            var contentChanged = !HasContent(current, resource.Content);
            var displayNameChanged = resource.DisplayName is not null
                && !string.Equals(resource.DisplayName, current.DisplayName, StringComparison.Ordinal);

            if (!contentChanged && !displayNameChanged)
            {
                plan.Add(published is not null && !(published.TryGetValue(resource.Name, out var live) && HasContent(live, resource.Content))
                    ? new WebResourcePlanItem(resource, PlannedAction.Unchanged, current.Id, "saved, but not published", PublishPending: true)
                    : new WebResourcePlanItem(resource, PlannedAction.Unchanged, current.Id, "content identical"));
                continue;
            }

            var reasons = new List<string>();
            if (contentChanged) reasons.Add("content changed");
            if (displayNameChanged) reasons.Add($"display name '{current.DisplayName}' -> '{resource.DisplayName}'");
            plan.Add(new WebResourcePlanItem(resource, PlannedAction.Update, current.Id, string.Join(", ", reasons), contentChanged, displayNameChanged));
        }

        return plan;
    }

    public static bool HasContent(ExistingWebResource resource, byte[] content) =>
        resource.ContentBase64 is not null && Convert.FromBase64String(resource.ContentBase64).AsSpan().SequenceEqual(content);
}
