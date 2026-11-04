using SupportTicket.DeploymentHelper.Configuration;

namespace SupportTicket.DeploymentHelper.Frontend;

public sealed record FrontendBuildResult(bool Succeeded, IReadOnlyList<string> Messages);

/// <summary>Builds the TypeScript Web Resources before they are deployed.</summary>
public interface IFrontendBuild
{
    Task<FrontendBuildResult> BuildAsync(DeploymentManifest manifest, CancellationToken cancellationToken);
}

/// <summary>Finds the npm executable on PATH (npm.cmd on Windows).</summary>
public static class NpmLocator
{
    public static string? Find(string? pathVariable, bool isWindows, Func<string, bool> fileExists)
    {
        var fileName = isWindows ? "npm.cmd" : "npm";
        return (pathVariable ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory.Trim('"'), fileName))
            .FirstOrDefault(fileExists);
    }

    public static string? Find() => Find(Environment.GetEnvironmentVariable("PATH"), OperatingSystem.IsWindows(), File.Exists);
}

/// <summary>
/// Runs the existing npm workspace scripts (type check, then build) at the repository root and
/// checks that every Web Resource in the manifest was written by this build. Any failure stops
/// the deployment, so a stale bundle is never uploaded. Never installs or updates packages.
/// </summary>
public sealed class FrontendBuild : IFrontendBuild
{
    public static readonly TimeSpan StepTimeout = TimeSpan.FromMinutes(5);

    // File systems may round timestamps; allow a little slack when comparing with the build start.
    private static readonly TimeSpan TimestampTolerance = TimeSpan.FromSeconds(2);

    private readonly IProcessRunner _runner;
    private readonly ManifestValidator _validator;
    private readonly string _repositoryRoot;
    private readonly Func<string?> _findNpm;
    private readonly Func<DateTime> _utcNow;

    public FrontendBuild(IProcessRunner runner, ManifestValidator validator, string repositoryRoot)
        : this(runner, validator, repositoryRoot, NpmLocator.Find, () => DateTime.UtcNow)
    {
    }

    internal FrontendBuild(IProcessRunner runner, ManifestValidator validator, string repositoryRoot, Func<string?> findNpm, Func<DateTime> utcNow)
    {
        _runner = runner;
        _validator = validator;
        _repositoryRoot = repositoryRoot;
        _findNpm = findNpm;
        _utcNow = utcNow;
    }

    public async Task<FrontendBuildResult> BuildAsync(DeploymentManifest manifest, CancellationToken cancellationToken)
    {
        var messages = new List<string>();
        if (manifest.WebResources.Count == 0)
            return new FrontendBuildResult(true, ["No Web Resources in the manifest; nothing to build."]);

        var npm = _findNpm();
        if (npm is null)
            return Failed(messages, "npm was not found on PATH. Install Node.js (it includes npm), restart Visual Studio, and run 'npm install' once in the repository root.");

        var buildStarted = _utcNow();

        foreach (var (script, description) in new[] { ("typecheck", "TypeScript type check"), ("build", "TypeScript compile and esbuild bundle") })
        {
            var request = new ProcessRequest(npm, ["run", script], _repositoryRoot, StepTimeout);
            messages.Add($"{description}: {request} ...");

            ProcessResult result;
            try
            {
                result = await _runner.RunAsync(request, cancellationToken);
            }
            catch (FileNotFoundException ex)
            {
                return Failed(messages, ex.Message);
            }

            if (result.TimedOut)
                return Failed(messages, $"{description} did not finish within {StepTimeout.TotalMinutes:0} minutes and was stopped.", result);
            if (result.ExitCode != 0)
                return Failed(messages, $"{description} failed with exit code {result.ExitCode}.", result);

            messages.Add($"{description}: passed.");
        }

        var stale = new List<string>();
        foreach (var entry in manifest.WebResources)
        {
            var path = _validator.ResolveInRepository(entry.File);
            if (path is null || !File.Exists(path))
                stale.Add($"{entry.Name}: {entry.File} was not produced by the build.");
            else if (File.GetLastWriteTimeUtc(path) < buildStarted - TimestampTolerance)
                stale.Add($"{entry.Name}: {entry.File} was not rewritten by this build (stale bundle).");
        }

        if (stale.Count > 0)
        {
            messages.AddRange(stale);
            return Failed(messages, "Built output does not match the manifest; nothing was deployed.");
        }

        messages.Add($"All {manifest.WebResources.Count} manifest bundle(s) were produced by this build.");
        return new FrontendBuildResult(true, messages);
    }

    private static FrontendBuildResult Failed(List<string> messages, string reason, ProcessResult? result = null)
    {
        messages.Add(reason);
        if (result is not null)
            messages.AddRange(Tail(result.StandardError + Environment.NewLine + result.StandardOutput));
        return new FrontendBuildResult(false, messages);
    }

    /// <summary>The last lines of the build output, enough to see the error without flooding the console.</summary>
    internal static IEnumerable<string> Tail(string output, int lines = 40) =>
        output.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Trim().Length > 0).TakeLast(lines).Select(l => "    " + l);
}
