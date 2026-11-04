using SupportTicket.DeploymentHelper.Configuration;

namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>Resolves and validates the main solution before any planning; shared by all targets.</summary>
public static class TargetSolution
{
    /// <summary>Returns the validated solution, or null after printing why it cannot be used.</summary>
    public static SolutionInfo? Resolve(SolutionRepository solutions, DeploymentSettings settings, IUserPrompt prompt)
    {
        var solution = solutions.GetSolution(settings.SolutionUniqueName);
        var errors = SolutionValidator.Validate(settings.SolutionUniqueName, settings.PublisherPrefix, solution);
        if (errors.Count == 0) return solution;

        prompt.WriteLine("The target solution cannot be used; nothing was planned or deployed:");
        foreach (var error in errors) prompt.WriteLine($"  - {error}");
        return null;
    }

    public static IEnumerable<string> Header(string environmentName, SolutionInfo solution)
    {
        yield return $"Environment: {environmentName}";
        yield return $"Solution: {solution.UniqueName} ({solution.FriendlyName}, unmanaged, publisher {solution.PublisherPrefix})";
        yield return string.Empty;
    }

    public static string Describe(SolutionAssociation association, string solutionUniqueName) => association.Action switch
    {
        AssociationAction.CreatedInSolution => $"[Solution] {association.Label} -> {solutionUniqueName} (created in the solution)",
        _ => $"[Solution] {association.Label} -> {solutionUniqueName} (add existing component)",
    };

    public static void PrintVerification(IUserPrompt prompt, IReadOnlyList<string> issues, string solutionUniqueName)
    {
        prompt.WriteLine();
        if (issues.Count == 0)
        {
            prompt.WriteLine($"Verification: all deployed components were found in Dataverse and in {solutionUniqueName}.");
            return;
        }

        prompt.WriteLine($"VERIFICATION FAILED ({issues.Count} issue(s)); the deployment is not complete:");
        foreach (var issue in issues) prompt.WriteLine($"  - {issue}");
    }
}
