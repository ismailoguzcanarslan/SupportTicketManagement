using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Forms;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Forms;

public class FormSelectionTests
{
    private static readonly DiscoveredForm Information = new(Guid.NewGuid(), "Information", "ogz_support_ticket", true, true);
    private static readonly DiscoveredForm Agent = new(Guid.NewGuid(), "Agent", "ogz_support_ticket", false, true);
    private static readonly DiscoveredForm[] TwoForms = [Information, Agent];

    [Theory]
    [InlineData("1", new[] { 0 })]
    [InlineData("2", new[] { 1 })]
    [InlineData("1,2", new[] { 0, 1 })]
    [InlineData(" 2 , 1 ", new[] { 1, 0 })]
    [InlineData("1,1", new[] { 0 })]
    public void Parse_AcceptsListedNumbers(string input, int[] expected)
    {
        var result = FormSelection.Parse(input, TwoForms);

        Assert.Null(result.Error);
        Assert.Equal(expected.Select(i => TwoForms[i]), result.Selected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Parse_EmptyInputSelectsNothing(string? input)
    {
        var result = FormSelection.Parse(input, TwoForms);

        Assert.Null(result.Error);
        Assert.Empty(result.Selected);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("3")]
    [InlineData("-1")]
    [InlineData("a")]
    [InlineData("1,x")]
    [InlineData("1,,2")]
    [InlineData("all")]
    public void Parse_RejectsAnythingButListedNumbers(string input)
    {
        var result = FormSelection.Parse(input, TwoForms);

        Assert.NotNull(result.Error);
        Assert.Empty(result.Selected);
    }

    [Fact]
    public void Resolver_KeepsExplicitFormIds_WithoutDiscovery()
    {
        var discovery = new FakeDiscovery();
        var explicitForm = Entry(formId: Guid.NewGuid().ToString());

        var resolved = new FormTargetResolver(discovery, new Prompt()).Resolve([explicitForm]);

        Assert.Same(explicitForm, Assert.Single(resolved));
        Assert.Equal(0, discovery.Queries);
    }

    [Fact]
    public void Resolver_WithZeroForms_SelectsNothingAndSaysSo()
    {
        var prompt = new Prompt("1");

        var resolved = new FormTargetResolver(new FakeDiscovery(), prompt).Resolve([Entry()]);

        Assert.Empty(resolved);
        Assert.Contains(prompt.Output, l => l.StartsWith("No active main forms found for ogz_support_ticket"));
        Assert.Equal(0, prompt.Questions);
    }

    [Fact]
    public void Resolver_WithOneForm_StillAsks_NeverSelectsAutomatically()
    {
        var prompt = new Prompt("");

        var resolved = new FormTargetResolver(new FakeDiscovery(Information), prompt).Resolve([Entry()]);

        Assert.Empty(resolved);
        Assert.Equal(1, prompt.Questions);
        Assert.Contains($"  1 - Information ({Information.Id}) [default]", prompt.Output);
    }

    [Fact]
    public void Resolver_WithOneForm_UsesItWhenSelected()
    {
        var resolved = new FormTargetResolver(new FakeDiscovery(Information), new Prompt("1")).Resolve([Entry()]);

        var form = Assert.Single(resolved);
        Assert.Equal(Information.Id.ToString(), form.FormId);
        Assert.Equal("ogz_/forms/supportTicket.js", form.Library);
        Assert.Equal("Ogz.Forms.SupportTicket.onLoad", Assert.Single(form.Events).Function);
    }

    [Fact]
    public void Resolver_WithMultipleForms_UsesEverySelectedForm_AndShowsThem()
    {
        var prompt = new Prompt("1,2");

        var resolved = new FormTargetResolver(new FakeDiscovery(Information, Agent), prompt).Resolve([Entry()]);

        Assert.Equal([Information.Id.ToString(), Agent.Id.ToString()], resolved.Select(f => f.FormId));
        Assert.Contains($"[Selected] form 'Information' ({Information.Id}) gets ogz_/forms/supportTicket.js", prompt.Output);
        Assert.Contains($"[Selected] form 'Agent' ({Agent.Id}) gets ogz_/forms/supportTicket.js", prompt.Output);
    }

    [Fact]
    public void Resolver_AsksAgainAfterAnInvalidSelection()
    {
        var prompt = new Prompt("5", "2");

        var resolved = new FormTargetResolver(new FakeDiscovery(Information, Agent), prompt).Resolve([Entry()]);

        Assert.Equal(Agent.Id.ToString(), Assert.Single(resolved).FormId);
        Assert.Contains("'5' is not a form number between 1 and 2.", prompt.Output);
    }

    [Fact]
    public void Resolver_GivesUpAfterThreeInvalidAnswers_OrAtEndOfInput()
    {
        Assert.Empty(new FormTargetResolver(new FakeDiscovery(Information), new Prompt("x", "y", "z", "1")).Resolve([Entry()]));
        Assert.Empty(new FormTargetResolver(new FakeDiscovery(Information), new Prompt()).Resolve([Entry()]));
    }

    [Fact]
    public void Resolver_QueriesTheConfiguredTableAndFormType()
    {
        var discovery = new FakeDiscovery(Information);

        new FormTargetResolver(discovery, new Prompt("")).Resolve([Entry(formType: "quickCreate")]);

        Assert.Equal(("ogz_support_ticket", FormTypes.QuickCreate), discovery.LastQuery);
    }

    [Fact]
    public void Manifest_AcceptsDiscoveryEntries_AndRejectsUnknownFormTypes()
    {
        using var repo = new TestRepository();
        repo.WriteFile("dist/supportTicket.js");
        var validator = new ManifestValidator("ogz", repo.Root);
        DeploymentManifest Manifest(FormEntry form) => new()
        {
            WebResources = [new WebResourceEntry { Name = "ogz_/forms/supportTicket.js", File = "dist/supportTicket.js" }],
            Forms = [form],
        };

        Assert.Empty(validator.ValidateWebResources(Manifest(Entry())));
        Assert.Contains(validator.ValidateWebResources(Manifest(Entry(formType: "dashboard"))), e => e.Contains("formType must be 'main' or 'quickCreate'"));
        Assert.Contains(validator.ValidateWebResources(Manifest(Entry(formType: ""))), e => e.Contains("formType must be"));
        Assert.Contains(validator.ValidateWebResources(Manifest(Entry(formId: "Information"))), e => e.Contains("formId must be an explicit form GUID"));
    }

    private static FormEntry Entry(string formId = "", string formType = "main") => new()
    {
        Entity = "ogz_support_ticket",
        FormId = formId,
        FormType = formType,
        Library = "ogz_/forms/supportTicket.js",
        Events = [new FormEventEntry { Event = "onload", Function = "Ogz.Forms.SupportTicket.onLoad" }],
    };

    private sealed class FakeDiscovery(params DiscoveredForm[] forms) : IFormDiscovery
    {
        public int Queries { get; private set; }
        public (string Entity, int Type)? LastQuery { get; private set; }

        public IReadOnlyList<DiscoveredForm> FindForms(string entity, int formType)
        {
            Queries++;
            LastQuery = (entity, formType);
            return forms;
        }
    }

    private sealed class Prompt(params string[] answers) : IUserPrompt
    {
        private readonly Queue<string> _answers = new(answers);

        public List<string> Output { get; } = [];
        public int Questions { get; private set; }

        public void WriteLine(string text = "") => Output.Add(text);

        public string? ReadLine(string prompt)
        {
            Questions++;
            return _answers.Count > 0 ? _answers.Dequeue() : null;
        }
    }
}
