using SupportTicket.DeploymentHelper.AzureFunctions;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Plugins;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Deployment;

public class DeploymentConsoleTests
{
    [Fact]
    public void Menu_MatchesTheAgreedLayout()
    {
        var console = new DeploymentConsole([new FakeTarget(2, "Deploy Plug-ins & Actions"), new FakeTarget(1, "Deploy Web Resources"), new AzureFunctionsDeploymentTarget()], new ScriptedPrompt());

        Assert.Equal(
            [
                "====================================",
                " SupportTicket Deployment Helper",
                "====================================",
                "",
                "1 - Deploy Web Resources",
                "2 - Deploy Plug-ins & Actions",
                "3 - Deploy Azure Functions (Coming Soon)",
                "0 - Exit",
                "",
            ],
            console.RenderMenu());
    }

    [Fact]
    public async Task NothingRunsUntilSelected_AndZeroExits()
    {
        var target = new FakeTarget(1, "Deploy Web Resources");

        var exit = await new DeploymentConsole([target], new ScriptedPrompt("0")).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(0, target.Runs);
    }

    [Fact]
    public async Task SelectedTargetRuns_WithDryRunOption()
    {
        var target = new FakeTarget(1, "Deploy Web Resources");

        await new DeploymentConsole([target], new ScriptedPrompt("1", "0")).RunAsync(new DeploymentRunOptions(true), CancellationToken.None);

        Assert.Equal(1, target.Runs);
        Assert.True(target.LastOptions!.DryRunOnly);
    }

    [Fact]
    public async Task ComingSoonTarget_IsNotRun()
    {
        var prompt = new ScriptedPrompt("3", "0");

        await new DeploymentConsole([new AzureFunctionsDeploymentTarget()], prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains("Deploy Azure Functions is not available yet.", prompt.Output);
    }

    [Fact]
    public async Task FailingTarget_IsReported_AndMenuContinues()
    {
        var prompt = new ScriptedPrompt("1", "0");
        var failing = new FakeTarget(1, "Deploy Web Resources", new InvalidOperationException("boom"));

        var exit = await new DeploymentConsole([failing], prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(1, exit);
        Assert.Contains("Deploy Web Resources failed: boom", prompt.Output);
    }

    [Fact]
    public async Task EndOfInput_Exits_InsteadOfWaiting()
    {
        var exit = await new DeploymentConsole([new FakeTarget(1, "x")], new ScriptedPrompt()).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(0, exit);
    }

    [Theory]
    [InlineData("DEV", true)]
    [InlineData("dev", false)]
    [InlineData("y", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Confirmation_RequiresTypingTheEnvironmentName(string? answer, bool expected)
    {
        Assert.Equal(expected, new ScriptedPrompt(answer).ConfirmByTypingEnvironment("Deploy?", "DEV"));
    }

    [Fact]
    public void PackageFile_PicksHighestVersion()
    {
        using var repo = new TestRepository();
        repo.WriteFile("ogz_Pkg.1.0.9.nupkg");
        repo.WriteFile("ogz_Pkg.1.0.10.nupkg");
        repo.WriteFile("other.2.0.0.nupkg");

        Assert.Equal("1.0.10", PluginPackageFile.FindLatest(repo.Root, "ogz_Pkg").Version);
    }

    private sealed class FakeTarget(int key, string title, Exception? error = null) : IDeploymentTarget
    {
        public int MenuKey => key;
        public string Title => title;
        public bool IsAvailable => true;
        public int Runs { get; private set; }
        public DeploymentRunOptions? LastOptions { get; private set; }

        public Task RunAsync(DeploymentRunOptions options, CancellationToken cancellationToken)
        {
            Runs++;
            LastOptions = options;
            return error is null ? Task.CompletedTask : Task.FromException(error);
        }
    }

    private sealed class ScriptedPrompt(params string?[] answers) : IUserPrompt
    {
        private readonly Queue<string?> _answers = new(answers);

        public List<string> Output { get; } = [];

        public void WriteLine(string text = "") => Output.Add(text);

        public string? ReadLine(string prompt) => _answers.Count > 0 ? _answers.Dequeue() : null;
    }
}
