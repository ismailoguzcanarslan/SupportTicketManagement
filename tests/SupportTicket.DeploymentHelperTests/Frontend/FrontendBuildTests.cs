using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Frontend;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Frontend;

public sealed class FrontendBuildTests : IDisposable
{
    private const string Npm = @"C:\nodejs\npm.cmd";
    private const string Bundle = "src/frontend/crm-client/dist/webresources/forms/supportTicket.js";

    private readonly TestRepository _repo = new();
    private readonly FakeRunner _runner = new();
    private DateTime _now = DateTime.UtcNow;

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task RunsTypeCheckThenBuild_InTheRepositoryRoot_AndSucceedsWhenBundlesAreFresh()
    {
        _runner.OnBuild = () => _repo.WriteFile(Bundle);

        var result = await Build().BuildAsync(Manifest(), CancellationToken.None);

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Messages));
        Assert.Equal([["run", "typecheck"], ["run", "build"]], _runner.Requests.Select(r => r.Arguments));
        Assert.All(_runner.Requests, r => Assert.Equal(Npm, r.FileName));
        Assert.All(_runner.Requests, r => Assert.Equal(_repo.Root, r.WorkingDirectory));
        Assert.All(_runner.Requests, r => Assert.Equal(FrontendBuild.StepTimeout, r.Timeout));
    }

    [Fact]
    public async Task TypeCheckFailure_StopsBeforeBundling_AndShowsTheCompilerOutput()
    {
        _runner.Results["typecheck"] = new ProcessResult(2, "src/forms/supportTicket.ts(10,5): error TS2322: Type 'string' is not assignable to type 'number'.", "", false);

        var result = await Build().BuildAsync(Manifest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Single(_runner.Requests);
        Assert.Contains(result.Messages, m => m.Contains("type check failed with exit code 2"));
        Assert.Contains(result.Messages, m => m.Contains("error TS2322"));
    }

    [Fact]
    public async Task BundleFailure_FailsWithTheEsbuildError()
    {
        _runner.Results["build"] = new ProcessResult(1, "", "✘ [ERROR] Could not resolve \"./missing.js\"", false);

        var result = await Build().BuildAsync(Manifest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, m => m.Contains("Could not resolve"));
    }

    [Fact]
    public async Task MissingNpm_FailsWithInstallInstructions_WithoutRunningAnything()
    {
        var build = new FrontendBuild(_runner, Validator(), _repo.Root, () => null, () => _now);

        var result = await build.BuildAsync(Manifest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, m => m.Contains("npm was not found on PATH"));
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public async Task NpmThatCannotStart_FailsCleanly()
    {
        _runner.Throw = new FileNotFoundException("Could not start 'npm.cmd'");

        var result = await Build().BuildAsync(Manifest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, m => m.Contains("Could not start"));
    }

    [Fact]
    public async Task Timeout_FailsAndSaysTheStepWasStopped()
    {
        _runner.Results["typecheck"] = new ProcessResult(null, "", "", TimedOut: true);

        var result = await Build().BuildAsync(Manifest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, m => m.Contains("did not finish within 5 minutes"));
    }

    [Fact]
    public async Task MissingBundle_AfterASuccessfulBuild_Fails()
    {
        var result = await Build().BuildAsync(Manifest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, m => m.Contains("was not produced by the build"));
    }

    [Fact]
    public async Task StaleBundle_NotRewrittenByThisBuild_Fails()
    {
        var path = _repo.WriteFile(Bundle);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-1));
        _now = DateTime.UtcNow;

        var result = await Build().BuildAsync(Manifest(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains(result.Messages, m => m.Contains("stale bundle"));
    }

    [Fact]
    public async Task EmptyManifest_BuildsNothing()
    {
        var result = await Build().BuildAsync(new DeploymentManifest(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Empty(_runner.Requests);
    }

    [Fact]
    public void NpmLocator_FindsNpmCmdOnWindowsAndNpmElsewhere()
    {
        var path = string.Join(Path.PathSeparator, @"C:\tools", "\"C:\\Program Files\\nodejs\"");
        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.Combine(@"C:\Program Files\nodejs", "npm.cmd") };

        Assert.Equal(Path.Combine(@"C:\Program Files\nodejs", "npm.cmd"), NpmLocator.Find(path, isWindows: true, existing.Contains));
        Assert.Null(NpmLocator.Find(path, isWindows: false, existing.Contains));
        Assert.Null(NpmLocator.Find(null, isWindows: true, _ => true));
    }

    [Fact]
    public void Tail_KeepsTheLastNonEmptyLines()
    {
        var output = string.Join("\r\n", Enumerable.Range(1, 50).Select(i => $"line {i}")) + "\r\n\r\n";

        var tail = FrontendBuild.Tail(output, lines: 3).ToList();

        Assert.Equal(["    line 48", "    line 49", "    line 50"], tail);
    }

    private FrontendBuild Build() => new(_runner, Validator(), _repo.Root, () => Npm, () => _now);

    private ManifestValidator Validator() => new("ogz", _repo.Root);

    private static DeploymentManifest Manifest() =>
        new() { WebResources = [new WebResourceEntry { Name = "ogz_/forms/supportTicket.js", File = Bundle }] };

    private sealed class FakeRunner : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];
        public Dictionary<string, ProcessResult> Results { get; } = [];
        public Action? OnBuild { get; set; }
        public Exception? Throw { get; set; }

        public Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Throw is not null) throw Throw;

            var script = request.Arguments[^1];
            if (script == "build") OnBuild?.Invoke();
            return Task.FromResult(Results.GetValueOrDefault(script) ?? new ProcessResult(0, "ok", "", false));
        }
    }
}
