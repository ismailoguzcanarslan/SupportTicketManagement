using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.Plugins;

public static class PluginLabels
{
    public const string Package = "Plugin Package";

    public static string Step(DeclaredStep step) =>
        $"Plugin Step - {step.MessageName} / {step.EntityLogicalName ?? "(unbound)"} ({StepStage.Name(step.Stage)}, {ShortTypeName(step.PluginTypeName)})";

    public static string ShortTypeName(string typeName) => typeName[(typeName.LastIndexOf('.') + 1)..];
}

/// <summary>
/// Plans which plug-in components join the target solution. Pure: works on the registration plan and
/// the current solution members.
/// </summary>
/// <remarks>
/// Added independently: the plug-in package and each step (with its images as subcomponents).
/// Not added independently: plug-in assemblies and types are created and owned by the package, and
/// Custom APIs are not owned by this deployment, so they are never added.
/// </remarks>
public static class PluginSolutionPlanner
{
    public static IReadOnlyList<SolutionAssociation> Plan(
        PluginRegistrationPlan plan,
        IReadOnlyDictionary<Guid, SolutionMember> members,
        int packageComponentType)
    {
        var associations = new List<SolutionAssociation>();
        if (plan.IsBlocked) return associations;

        if (plan.Package.Action == PlannedAction.Create)
            associations.Add(SolutionAssociation.ForCreate(PluginLabels.Package, packageComponentType));
        else if (plan.Package.ExistingId is { } packageId
                 && SolutionAssociation.ForExisting(PluginLabels.Package, packageComponentType, packageId, members) is { } package)
            associations.Add(package);

        foreach (var step in plan.Steps)
        {
            var label = PluginLabels.Step(step.Declared);
            if (step.Action == PlannedAction.Create)
                associations.Add(SolutionAssociation.ForCreate(label, SolutionComponents.SdkMessageProcessingStep));
            else if (step.Action != PlannedAction.Blocked
                     && step.ExistingStepId is { } stepId
                     && SolutionAssociation.ForExisting(label, SolutionComponents.SdkMessageProcessingStep, stepId, members) is { } existing)
                associations.Add(existing);
        }

        return associations;
    }
}

/// <summary>Checks, from a fresh read of Dataverse, that a plug-in deployment really landed. Pure.</summary>
public static class PluginDeploymentVerifier
{
    public static IReadOnlyList<string> Verify(
        IEnumerable<StepPlan> applied,
        RegistrationSnapshot after,
        IReadOnlyDictionary<Guid, SolutionMember> members,
        string solutionUniqueName)
    {
        var issues = new List<string>();

        if (after.Package is not { } package)
            return ["The plug-in package was not found after deployment."];
        if (!members.ContainsKey(package.Id))
            issues.Add($"The plug-in package is not in solution {solutionUniqueName}.");

        foreach (var plan in applied.Where(s => s.Action != PlannedAction.Blocked))
        {
            var declared = plan.Declared;
            var label = PluginLabels.Step(declared);

            if (!after.PluginTypes.TryGetValue(declared.PluginTypeName, out var typeId))
            {
                issues.Add($"Plug-in type {declared.PluginTypeName} is not in the package.");
                continue;
            }

            var step = after.Steps.FirstOrDefault(s => s.PluginTypeId == typeId && PluginRegistrationPlanner.Matches(s, declared));
            if (step is null)
            {
                issues.Add($"{label}: step not found.");
                continue;
            }

            members.TryGetValue(step.Id, out var stepMember);
            if (stepMember is null)
                issues.Add($"{label}: step is not in solution {solutionUniqueName}.");

            foreach (var image in declared.Images)
            {
                var existing = step.Images.FirstOrDefault(i => i.ImageType == image.ImageType && string.Equals(i.Alias, image.Alias, StringComparison.OrdinalIgnoreCase));
                if (existing is null)
                    issues.Add($"{label}: image {image.Alias} not found.");
                else if (!members.ContainsKey(existing.Id) && stepMember?.IncludesSubcomponents != true)
                    issues.Add($"{label}: image {image.Alias} is not in solution {solutionUniqueName}.");
            }
        }

        return issues;
    }
}
