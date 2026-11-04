namespace SupportTicket.DeploymentHelper.Deployment;

public enum PlannedAction
{
    Create,
    Update,
    Unchanged,

    /// <summary>Cannot be deployed safely; needs a manual fix. Never applied.</summary>
    Blocked,
}

/// <summary>Outcome of one deployment run: what was created, updated, left unchanged or failed.</summary>
public sealed class DeploymentReport
{
    private readonly List<(string Status, string Item)> _entries = [];

    public int Count(string status) => _entries.Count(e => e.Status == status);

    public void Created(string item) => _entries.Add(("created", item));

    public void Updated(string item) => _entries.Add(("updated", item));

    public void Unchanged(string item) => _entries.Add(("unchanged", item));

    public void Skipped(string item) => _entries.Add(("skipped", item));

    public void Failed(string item, Exception error) => _entries.Add(("failed", $"{item}: {error.Message}"));

    public bool HasFailures => Count("failed") > 0;

    public void Print(IUserPrompt prompt)
    {
        prompt.WriteLine();
        foreach (var (status, item) in _entries.Where(e => e.Status is "failed" or "skipped"))
        {
            prompt.WriteLine($"  {status.ToUpperInvariant()}: {item}");
        }

        prompt.WriteLine(
            $"Result: {Count("created")} created, {Count("updated")} updated, {Count("unchanged")} unchanged, " +
            $"{Count("skipped")} skipped, {Count("failed")} failed.");
    }
}
