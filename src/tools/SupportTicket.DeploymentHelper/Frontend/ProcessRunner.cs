using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace SupportTicket.DeploymentHelper.Frontend;

public sealed record ProcessRequest(string FileName, IReadOnlyList<string> Arguments, string WorkingDirectory, TimeSpan Timeout)
{
    public override string ToString() => $"{Path.GetFileName(FileName)} {string.Join(" ", Arguments)}";
}

/// <param name="ExitCode">Null when the process timed out and was killed.</param>
public sealed record ProcessResult(int? ExitCode, string StandardOutput, string StandardError, bool TimedOut)
{
    public bool Succeeded => !TimedOut && ExitCode == 0;
}

/// <summary>Runs an external process; abstracted so build orchestration is testable without npm.</summary>
public interface IProcessRunner
{
    /// <exception cref="FileNotFoundException">The executable cannot be started.</exception>
    Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Starts a process without a shell, captures stdout and stderr concurrently (so a full pipe never
/// blocks the child) and kills the whole process tree on timeout or cancellation.
/// </summary>
public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(ProcessRequest request, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(request.FileName)
        {
            WorkingDirectory = request.WorkingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in request.Arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            throw new FileNotFoundException($"Could not start '{request.FileName}': {ex.Message}", request.FileName, ex);
        }

        process.StandardInput.Close(); // the build must never wait for input
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(request.Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            await process.WaitForExitAsync(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
            return new ProcessResult(null, await output, await error, TimedOut: true);
        }

        return new ProcessResult(process.ExitCode, await output, await error, TimedOut: false);
    }
}
