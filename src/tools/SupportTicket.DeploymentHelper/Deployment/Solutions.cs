namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>The target solution as stored in Dataverse.</summary>
public sealed record SolutionInfo(Guid Id, string UniqueName, string FriendlyName, bool IsManaged, string PublisherPrefix);

/// <summary>A solutioncomponent row: a component already in the solution.</summary>
/// <param name="RootComponentBehavior">0 = include subcomponents, 1 = do not include, 2 = include as shell.</param>
public sealed record SolutionMember(Guid ObjectId, int ComponentType, int RootComponentBehavior)
{
    public bool IncludesSubcomponents => RootComponentBehavior == 0;
}

public enum AssociationAction
{
    /// <summary>The component is created with SolutionUniqueName, so it is in the solution from the start.</summary>
    CreatedInSolution,

    /// <summary>An existing component is added with AddSolutionComponent.</summary>
    Add,
}

/// <summary>A planned solution membership change. Components already in the solution get none.</summary>
public sealed record SolutionAssociation(string Label, int ComponentType, Guid? ObjectId, AssociationAction Action)
{
    public static SolutionAssociation ForCreate(string label, int componentType) =>
        new(label, componentType, null, AssociationAction.CreatedInSolution);

    /// <summary>Null when the component is already a member, so it is never added twice.</summary>
    public static SolutionAssociation? ForExisting(string label, int componentType, Guid objectId, IReadOnlyDictionary<Guid, SolutionMember> members) =>
        members.ContainsKey(objectId) ? null : new(label, componentType, objectId, AssociationAction.Add);
}

/// <summary>
/// Forms are subcomponents of their table. A table in the solution with all its subcomponents
/// (rootcomponentbehavior 0, as when a table is created in the solution) contains every form of that
/// table without a solutioncomponent row per form; AddSolutionComponent for such a form succeeds but
/// adds no row. So a form is in the solution when it has its own row or its table includes it. Pure.
/// </summary>
public static class FormMembership
{
    /// <param name="tables">The solution's table rows, keyed by table logical name.</param>
    public static bool IsMember(Guid formId, string entity, IReadOnlyDictionary<Guid, SolutionMember> members, IReadOnlyDictionary<string, SolutionMember> tables) =>
        members.ContainsKey(formId) || IsIncludedByTable(entity, tables);

    public static bool IsIncludedByTable(string entity, IReadOnlyDictionary<string, SolutionMember> tables) =>
        tables.TryGetValue(entity, out var table) && table.IncludesSubcomponents;
}

public static class SolutionValidator
{
    private static readonly Guid DefaultSolutionId = Guid.Parse("fd140aaf-4df4-11dd-bd17-0019b9312238");

    private static readonly HashSet<string> SystemSolutions = new(StringComparer.OrdinalIgnoreCase)
    {
        "Default", "Active", "Basic", "System",
    };

    /// <summary>
    /// The solution must already exist (this tool never creates one), be unmanaged, not be a system
    /// solution such as the Default Solution, and belong to the configured publisher.
    /// </summary>
    public static IReadOnlyList<string> Validate(string expectedUniqueName, string publisherPrefix, SolutionInfo? solution)
    {
        if (string.IsNullOrWhiteSpace(expectedUniqueName))
            return ["solutionUniqueName is not set in deployment.settings.json."];

        if (SystemSolutions.Contains(expectedUniqueName))
            return [$"'{expectedUniqueName}' is a system solution; deploy into your own unmanaged solution instead."];

        if (solution is null)
            return [$"Solution '{expectedUniqueName}' does not exist in this environment. Create it in the maker portal first; this tool does not create solutions."];

        var errors = new List<string>();
        if (solution.Id == DefaultSolutionId)
            errors.Add($"'{solution.UniqueName}' is the Default Solution.");
        if (solution.IsManaged)
            errors.Add($"Solution '{solution.UniqueName}' is managed; components can only be deployed into an unmanaged development solution.");
        if (!string.Equals(solution.PublisherPrefix, publisherPrefix, StringComparison.OrdinalIgnoreCase))
            errors.Add($"Solution '{solution.UniqueName}' belongs to publisher prefix '{solution.PublisherPrefix}', expected '{publisherPrefix}'.");

        return errors;
    }
}
