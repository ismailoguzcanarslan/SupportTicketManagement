using System.Diagnostics;
using SupportTicket.DeploymentHelper.Frontend;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Frontend;

// Runs real (tiny) processes through cmd.exe; npm is started the same way (npm.cmd).
public class ProcessRunnerTests
{
    private static readonly string Cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");

    [Fact]
    public async Task CapturesExitCodeStdoutAndStderr()
    {
        if (!OperatingSystem.IsWindows()) return;

        var result = await Run(["/c", "echo out& echo err 1>&2& exit /b 3"], TimeSpan.FromSeconds(30));

        Assert.Equal(3, result.ExitCode);
        Assert.False(result.Succeeded);
        Assert.Contains("out", result.StandardOutput);
        Assert.Contains("err", result.StandardError);
    }

    [Fact]
    public async Task RunsInTheGivenWorkingDirectory()
    {
        if (!OperatingSystem.IsWindows()) return;
        var directory = Path.GetFullPath(Path.GetTempPath()).TrimEnd('\\');

        var result = await new ProcessRunner().RunAsync(new ProcessRequest(Cmd, ["/c", "cd"], directory, TimeSpan.FromSeconds(30)), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(directory, result.StandardOutput.Trim(), ignoreCase: true);
    }

    [Fact]
    public async Task KillsTheProcessTreeOnTimeout()
    {
        if (!OperatingSystem.IsWindows()) return;
        var stopwatch = Stopwatch.StartNew();

        var result = await Run(["/c", "ping -n 30 127.0.0.1 > nul"], TimeSpan.FromSeconds(1));

        Assert.True(result.TimedOut);
        Assert.Null(result.ExitCode);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20), $"took {stopwatch.Elapsed}");
    }

    [Fact]
    public async Task NeverWaitsForInput()
    {
        if (!OperatingSystem.IsWindows()) return;

        var result = await Run(["/c", "set /p answer=Question: "], TimeSpan.FromSeconds(10));

        Assert.False(result.TimedOut);
    }

    [Fact]
    public async Task MissingExecutable_ThrowsFileNotFound()
    {
        var request = new ProcessRequest(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid() + ".exe"), [], Path.GetTempPath(), TimeSpan.FromSeconds(5));

        await Assert.ThrowsAsync<FileNotFoundException>(() => new ProcessRunner().RunAsync(request, CancellationToken.None));
    }

    private static Task<ProcessResult> Run(string[] arguments, TimeSpan timeout) =>
        new ProcessRunner().RunAsync(new ProcessRequest(Cmd, arguments, Path.GetTempPath(), timeout), CancellationToken.None);
}
