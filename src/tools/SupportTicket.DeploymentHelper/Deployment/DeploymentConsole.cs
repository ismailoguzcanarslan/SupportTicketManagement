namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>The interactive menu. Nothing is deployed until the user picks a target and confirms its plan.</summary>
public sealed class DeploymentConsole
{
    private const string Rule = "====================================";

    private readonly IReadOnlyList<IDeploymentTarget> _targets;
    private readonly IUserPrompt _prompt;

    public DeploymentConsole(IEnumerable<IDeploymentTarget> targets, IUserPrompt prompt)
    {
        _targets = targets.OrderBy(t => t.MenuKey).ToList();
        _prompt = prompt;

        var duplicate = _targets.GroupBy(t => t.MenuKey).FirstOrDefault(g => g.Count() > 1 || g.Key == 0);
        if (duplicate is not null) throw new InvalidOperationException($"Menu key {duplicate.Key} is reserved or used twice.");
    }

    public IEnumerable<string> RenderMenu()
    {
        yield return Rule;
        yield return " SupportTicket Deployment Helper";
        yield return Rule;
        yield return string.Empty;
        foreach (var target in _targets)
        {
            yield return $"{target.MenuKey} - {target.Title}{(target.IsAvailable ? string.Empty : " (Coming Soon)")}";
        }

        yield return "0 - Exit";
        yield return string.Empty;
    }

    public async Task<int> RunAsync(DeploymentRunOptions options, CancellationToken cancellationToken)
    {
        var exitCode = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            foreach (var line in RenderMenu()) _prompt.WriteLine(line);

            var input = _prompt.ReadLine("Select an option: ");
            if (input is null || input.Trim() == "0") return exitCode;

            var target = int.TryParse(input.Trim(), out var key) ? _targets.FirstOrDefault(t => t.MenuKey == key) : null;
            if (target is null)
            {
                _prompt.WriteLine($"Unknown option '{input.Trim()}'.");
                continue;
            }

            if (!target.IsAvailable)
            {
                _prompt.WriteLine($"{target.Title} is not available yet.");
                _prompt.WriteLine();
                continue;
            }

            try
            {
                await target.RunAsync(options, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                exitCode = 1;
                _prompt.WriteLine($"{target.Title} failed: {ex.Message}");
            }

            _prompt.WriteLine();
        }

        return exitCode;
    }
}
