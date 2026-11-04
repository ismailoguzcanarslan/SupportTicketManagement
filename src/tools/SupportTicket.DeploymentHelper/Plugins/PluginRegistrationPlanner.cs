using System.Security.Cryptography;
using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.Plugins;

public sealed record PackagePlan(PlannedAction Action, string Version, string Reason, Guid? ExistingId = null);

public sealed record ImagePlan(DeclaredImage Declared, PlannedAction Action, Guid? ExistingId);

public sealed record StepPlan(
    DeclaredStep Declared,
    PlannedAction Action,
    Guid? ExistingStepId,
    IReadOnlyList<string> Differences,
    IReadOnlyList<ImagePlan> Images,
    string? BlockedReason);

public sealed record CustomApiLinkPlan(DeclaredStep Declared, CustomApiInfo CustomApi, PlannedAction Action, string? BlockedReason);

public sealed record PluginRegistrationPlan(
    PackagePlan Package,
    IReadOnlyList<StepPlan> Steps,
    IReadOnlyList<CustomApiLinkPlan> CustomApiLinks,
    IReadOnlyList<ExistingStep> ObsoleteSteps,
    IReadOnlyList<string> ObsoleteImages)
{
    public bool HasRegistrationChanges =>
        Package.Action is PlannedAction.Create or PlannedAction.Update
        || Steps.Any(s => s.Action is PlannedAction.Create or PlannedAction.Update
                          || s.Images.Any(i => i.Action is PlannedAction.Create or PlannedAction.Update));

    public bool HasCustomApiChanges => CustomApiLinks.Any(l => l.Action == PlannedAction.Update);

    /// <summary>The package itself cannot be deployed, so nothing in the plan may be applied.</summary>
    public bool IsBlocked => Package.Action == PlannedAction.Blocked;
}

/// <summary>
/// Compares declared registrations with the Dataverse snapshot. Pure: no I/O, so the same input
/// always produces the same plan, and a plan computed after a successful deployment is all Unchanged.
/// Obsolete registrations are reported, never deleted.
/// </summary>
public static class PluginRegistrationPlanner
{
    public static PluginRegistrationPlan Plan(
        IReadOnlyList<DeclaredStep> declared,
        RegistrationSnapshot snapshot,
        byte[] packageContent,
        string packageVersion,
        IReadOnlyCollection<string>? packagedPluginTypes = null)
    {
        var package = PlanPackage(snapshot, packageContent, packageVersion, packagedPluginTypes);
        var matched = new HashSet<Guid>();
        var steps = new List<StepPlan>();
        var links = new List<CustomApiLinkPlan>();
        var declaredKeys = declared.Where(s => !s.IsMainOperation).Select(s => s.Key).ToHashSet(StringComparer.Ordinal);

        foreach (var step in declared)
        {
            snapshot.PluginTypes.TryGetValue(step.PluginTypeName, out var pluginTypeId);
            var hasType = snapshot.PluginTypes.ContainsKey(step.PluginTypeName);

            if (!snapshot.Messages.TryGetValue(step.MessageName, out var message))
            {
                steps.Add(Blocked(step, $"SDK message '{step.MessageName}' does not exist. Create the Custom API or Custom Action first; this tool does not create message definitions."));
                continue;
            }

            if (step.IsMainOperation)
            {
                links.Add(PlanCustomApiLink(step, message, hasType ? pluginTypeId : null));
                continue;
            }

            if (message.CustomApi is { } api && BlockedForCustomApi(step, api, hasType ? pluginTypeId : null) is { } apiReason)
            {
                steps.Add(Blocked(step, apiReason));
                continue;
            }

            if (step.EntityLogicalName is { } entity
                && !snapshot.Filters.ContainsKey(RegistrationSnapshot.FilterKey(step.MessageName, entity)))
            {
                steps.Add(Blocked(step, $"Message '{step.MessageName}' is not available for table '{entity}'."));
                continue;
            }

            var foreign = snapshot.ForeignSteps.Where(f => f.PluginTypeName == step.PluginTypeName).ToList();
            if (foreign.Count > 0)
            {
                steps.Add(Blocked(step, $"{step.PluginTypeName} is also registered outside the package ({foreign[0].AssemblyName}, " +
                    $"{foreign.Count} step(s), e.g. '{foreign[0].StepName}'). Both would run; remove that registration in Dataverse first."));
                continue;
            }

            var existing = hasType
                ? snapshot.Steps.Where(s => s.PluginTypeId == pluginTypeId && Matches(s, step)).ToList()
                : [];

            if (existing.Count > 1)
            {
                steps.Add(Blocked(step, $"{existing.Count} matching steps already exist in Dataverse; remove the duplicates manually."));
                continue;
            }

            if (existing.Count == 0)
            {
                // Same plug-in, message and table at another stage that is no longer declared (e.g. the
                // stage was changed): the old step would keep running next to the new one.
                var stale = hasType
                    ? snapshot.Steps.FirstOrDefault(s => s.PluginTypeId == pluginTypeId
                        && SameMessageAndTable(s, step)
                        && !declaredKeys.Contains(StepKey.For(step.PluginTypeName, s.MessageName, AttributeList.NormalizeEntity(s.EntityLogicalName), s.Stage)))
                    : null;
                if (stale is not null)
                {
                    steps.Add(Blocked(step, $"step '{stale.Name}' ({StepStage.Name(stale.Stage)}) is still registered for the same message and table " +
                        "and would run as well; delete it in Dataverse first."));
                    continue;
                }

                steps.Add(new StepPlan(step, PlannedAction.Create, null, [],
                    step.Images.Select(i => new ImagePlan(i, PlannedAction.Create, null)).ToList(), null));
                continue;
            }

            var current = existing[0];
            matched.Add(current.Id);
            var differences = Differences(step, current);
            var images = step.Images.Select(i => PlanImage(i, current)).ToList();
            steps.Add(new StepPlan(step, differences.Count > 0 ? PlannedAction.Update : PlannedAction.Unchanged,
                current.Id, differences, images, null));
        }

        var obsoleteSteps = snapshot.Steps.Where(s => !matched.Contains(s.Id)).ToList();
        var obsoleteImages = steps
            .Where(s => s.ExistingStepId is not null)
            .SelectMany(s => snapshot.Steps.Single(e => e.Id == s.ExistingStepId).Images
                .Where(e => !s.Declared.Images.Any(d => d.ImageType == e.ImageType && AliasEquals(d.Alias, e.Alias)))
                .Select(e => $"{s.Declared.Name}: image '{e.Alias}'"))
            .ToList();

        return new PluginRegistrationPlan(package, steps, links, obsoleteSteps, obsoleteImages);
    }

    /// <remarks>
    /// Dataverse rules: a package's name and version cannot change after creation, and an update that
    /// removes plug-in types still used by steps is rejected. Both are blocked here, before uploading.
    /// </remarks>
    private static PackagePlan PlanPackage(
        RegistrationSnapshot snapshot,
        byte[] content,
        string version,
        IReadOnlyCollection<string>? packagedPluginTypes)
    {
        var existing = snapshot.Package;
        if (existing is null) return new PackagePlan(PlannedAction.Create, version, "new package");

        if (!string.Equals(existing.Version, version, StringComparison.Ordinal))
            return new PackagePlan(PlannedAction.Blocked, version,
                $"the package exists with version {existing.Version}, and Dataverse does not allow changing it; " +
                $"set <Version> in SupportTicket.Plugins.csproj back to {existing.Version}", existing.Id);

        if (packagedPluginTypes is not null)
        {
            var removed = snapshot.PluginTypes
                .Where(t => !packagedPluginTypes.Contains(t.Key) && snapshot.Steps.Any(s => s.PluginTypeId == t.Value))
                .Select(t => t.Key)
                .ToList();
            if (removed.Count > 0)
                return new PackagePlan(PlannedAction.Blocked, version,
                    $"the new package no longer contains {string.Join(", ", removed)}, which still has registered steps; " +
                    "Dataverse rejects such an update. Remove those steps first", existing.Id);
        }

        if (existing.Content is not { } remote)
            return new PackagePlan(PlannedAction.Update, version, "current content is not readable, so it is uploaded again", existing.Id);

        return SHA256.HashData(remote).AsSpan().SequenceEqual(SHA256.HashData(content))
            ? new PackagePlan(PlannedAction.Unchanged, version, "content identical", existing.Id)
            : new PackagePlan(PlannedAction.Update, version, "content changed", existing.Id);
    }

    private static CustomApiLinkPlan PlanCustomApiLink(DeclaredStep step, SdkMessageInfo message, Guid? pluginTypeId)
    {
        if (message.CustomApi is not { } api)
            return new CustomApiLinkPlan(step, new CustomApiInfo(message.Id, message.Name, null, 0, null), PlannedAction.Blocked,
                $"'{message.Name}' is not a Custom API; IsMainOperation only applies to Custom APIs.");

        if (!string.Equals(AttributeList.NormalizeEntity(api.BoundEntityLogicalName), step.EntityLogicalName, StringComparison.Ordinal))
            return new CustomApiLinkPlan(step, api, PlannedAction.Blocked,
                $"Custom API '{api.UniqueName}' is bound to '{api.BoundEntityLogicalName ?? "(unbound)"}', but the main handler declares '{step.EntityLogicalName ?? "(unbound)"}'.");

        if (api.PluginTypeId is null)
            return new CustomApiLinkPlan(step, api, PlannedAction.Update, null);

        return api.PluginTypeId == pluginTypeId
            ? new CustomApiLinkPlan(step, api, PlannedAction.Unchanged, null)
            : new CustomApiLinkPlan(step, api, PlannedAction.Blocked, $"Custom API '{api.UniqueName}' already has a different main handler.");
    }

    /// <summary>Rules for an ordinary step (not the main handler) on a Custom API message.</summary>
    private static string? BlockedForCustomApi(DeclaredStep step, CustomApiInfo api, Guid? pluginTypeId)
    {
        if (!string.Equals(AttributeList.NormalizeEntity(api.BoundEntityLogicalName), step.EntityLogicalName, StringComparison.Ordinal))
            return $"Custom API '{api.UniqueName}' is bound to '{api.BoundEntityLogicalName ?? "(unbound)"}', not '{step.EntityLogicalName ?? "(unbound)"}'.";

        if (pluginTypeId is not null && api.PluginTypeId == pluginTypeId)
            return $"{step.PluginTypeName} is already the main handler of Custom API '{api.UniqueName}'; a step would run it a second time. Use IsMainOperation instead.";

        return api.AllowedCustomProcessingStepType switch
        {
            0 => $"Custom API '{api.UniqueName}' does not allow custom processing steps. Use IsMainOperation to make this its main handler.",
            1 when !step.Async => $"Custom API '{api.UniqueName}' only allows asynchronous steps.",
            _ => null,
        };
    }

    internal static bool Matches(ExistingStep existing, DeclaredStep declared) =>
        SameMessageAndTable(existing, declared) && existing.Stage == declared.Stage;

    private static bool SameMessageAndTable(ExistingStep existing, DeclaredStep declared) =>
        string.Equals(existing.MessageName, declared.MessageName, StringComparison.OrdinalIgnoreCase)
        && string.Equals(AttributeList.NormalizeEntity(existing.EntityLogicalName), declared.EntityLogicalName, StringComparison.Ordinal);

    private static List<string> Differences(DeclaredStep declared, ExistingStep existing)
    {
        var differences = new List<string>();
        var mode = declared.Async ? 1 : 0;

        if (existing.Mode != mode) differences.Add($"mode {Mode(existing.Mode)} -> {Mode(mode)}");
        if (existing.Rank != declared.ExecutionOrder) differences.Add($"rank {existing.Rank} -> {declared.ExecutionOrder}");
        if (AttributeList.Normalize(existing.FilteringAttributes) != declared.FilteringAttributes)
            differences.Add($"filtering attributes '{existing.FilteringAttributes}' -> '{declared.FilteringAttributes}'");
        if (!string.Equals(existing.Name, declared.Name, StringComparison.Ordinal)) differences.Add($"name '{existing.Name}' -> '{declared.Name}'");
        if (!string.Equals(existing.Description ?? string.Empty, declared.Description ?? string.Empty, StringComparison.Ordinal)) differences.Add("description");
        if (declared.Async && existing.AsyncAutoDelete != declared.DeleteAsyncOperationIfSuccessful) differences.Add("delete system job on success");

        return differences;
    }

    private static ImagePlan PlanImage(DeclaredImage declared, ExistingStep step)
    {
        var existing = step.Images.FirstOrDefault(i => i.ImageType == declared.ImageType && AliasEquals(i.Alias, declared.Alias));
        if (existing is null) return new ImagePlan(declared, PlannedAction.Create, null);

        return AttributeList.Normalize(existing.Attributes) == declared.Attributes
            ? new ImagePlan(declared, PlannedAction.Unchanged, existing.Id)
            : new ImagePlan(declared, PlannedAction.Update, existing.Id);
    }

    private static bool AliasEquals(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static string Mode(int mode) => mode == 1 ? "async" : "sync";

    private static StepPlan Blocked(DeclaredStep step, string reason) =>
        new(step, PlannedAction.Blocked, null, [], [], reason);
}
