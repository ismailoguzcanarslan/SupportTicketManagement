using System.Text;
using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Forms;
using SupportTicket.DeploymentHelper.Frontend;
using SupportTicket.DeploymentHelper.WebResources;
using SupportTicket.DeploymentHelperTests.Fakes;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.WebResources;

// Option 1 end to end against an in-memory Dataverse: build -> plan -> confirm -> write -> verify.
public sealed class WebResourceDeploymentTargetTests : IDisposable
{
    private const string Solution = "OguzLabsSupportTicketManagement";
    private const string Name = "ogz_/forms/supportTicket.js";
    private const string File = "dist/forms/supportTicket.js";
    private const string Script = "var Ogz=Ogz||{};";

    private const string FormXml =
        """<form><tabs /><events><event name="onload" application="false" active="false"><Handlers><Handler functionName="Other.onLoad" libraryName="ogz_/other.js" handlerUniqueId="{11111111-1111-1111-1111-111111111111}" enabled="true" parameters="" passExecutionContext="true" /></Handlers></event></events><formLibraries><Library name="ogz_/other.js" libraryUniqueId="{22222222-2222-2222-2222-222222222222}" /></formLibraries></form>""";

    private readonly TestRepository _repo = new();
    private readonly InMemoryDataverse _dataverse = new();
    private readonly FakeBuild _build = new();
    private Guid _solutionId;

    public WebResourceDeploymentTargetTests()
    {
        _repo.WriteFile(File, Script);
        _solutionId = _dataverse.AddSolution(Solution);
    }

    public void Dispose() => _repo.Dispose();

    [Fact]
    public async Task DryRun_BuildsAndPlans_ButWritesNothing()
    {
        var prompt = new ScriptedPrompt();

        await Target(prompt).RunAsync(new DeploymentRunOptions(DryRunOnly: true), CancellationToken.None);

        Assert.True(_build.Ran);
        Assert.Empty(_dataverse.Writes);
        Assert.Contains($"[Create] Web Resource {Name} (new, 16 bytes)", prompt.Output);
        Assert.Contains($"[Solution] Web Resource {Name} -> {Solution} (created in the solution)", prompt.Output);
        Assert.Contains("Dry run: no changes made.", prompt.Output);
        Assert.Equal(0, prompt.Questions);
    }

    [Fact]
    public async Task FailedBuild_StopsBeforeDataverseIsTouched()
    {
        _build.Succeeds = false;
        var prompt = new ScriptedPrompt("DEV");

        await Target(prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(0, _dataverse.Calls);
        Assert.Contains("TypeScript build failed; nothing was deployed.", prompt.Output);
        Assert.Equal(0, prompt.Questions);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("managed")]
    [InlineData("publisher")]
    public async Task UnusableSolution_BlocksDeployment(string problem)
    {
        var dataverse = new InMemoryDataverse();
        if (problem == "managed") dataverse.AddSolution(Solution, isManaged: true);
        if (problem == "publisher") dataverse.AddSolution(Solution, publisherPrefix: "new");
        var prompt = new ScriptedPrompt("DEV");

        await Target(prompt, dataverse).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Empty(dataverse.Writes);
        Assert.Contains("The target solution cannot be used; nothing was planned or deployed:", prompt.Output);
        Assert.Equal(0, prompt.Questions);
    }

    [Theory]
    [InlineData("")]
    [InlineData("dev")]
    [InlineData("yes")]
    public async Task WithoutTypingDev_NothingIsWritten(string answer)
    {
        await Target(new ScriptedPrompt(answer)).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Empty(_dataverse.Writes);
    }

    [Fact]
    public async Task ConfirmedDeployment_CreatesInTheSolution_PublishesOnlyIt_AndVerifies()
    {
        var prompt = new ScriptedPrompt("DEV");

        await Target(prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        var created = _dataverse.FindWebResource(Name)!;
        Assert.Equal(Script, Encoding.UTF8.GetString(Convert.FromBase64String((string)created["content"])));
        Assert.True(_dataverse.IsMember(created.Id));
        Assert.Equal(["Create webresource", "PublishXml"], _dataverse.Writes);
        Assert.Contains($"<webresource>{created.Id:B}</webresource>", _dataverse.PublishedXml.Single());
        Assert.Contains($"Verification: all deployed components were found in Dataverse and in {Solution}.", prompt.Output);
    }

    [Fact]
    public async Task UnchangedWebResourceInTheSolution_IsNotWrittenOrPublished()
    {
        var id = _dataverse.AddWebResource(Name, Encoding.UTF8.GetBytes(Script));
        _dataverse.AddMember(_solutionId, id, SolutionComponents.WebResource);
        var prompt = new ScriptedPrompt("DEV");

        await Target(prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Empty(_dataverse.Writes);
        Assert.Contains($"Nothing to deploy: everything is up to date and in {Solution}.", prompt.Output);
    }

    [Fact]
    public async Task ChangedWebResourceOutsideTheSolution_IsUpdatedAddedAndPublished()
    {
        var id = _dataverse.AddWebResource(Name, Encoding.UTF8.GetBytes("old"));

        await Target(new ScriptedPrompt("DEV")).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(["Update webresource content", $"AddSolutionComponent 61 {id}", "PublishXml"], _dataverse.Writes);
        Assert.True(_dataverse.IsMember(id));
    }

    [Fact]
    public async Task FailedVerification_IsReportedExplicitly()
    {
        _dataverse.CreateJoinsSolution = false;
        var prompt = new ScriptedPrompt("DEV");

        await Target(prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains(prompt.Output, l => l.StartsWith("VERIFICATION FAILED"));
        Assert.Contains(prompt.Output, l => l.Contains($"Web Resource {Name}: not in solution {Solution}."));
        Assert.DoesNotContain(prompt.Output, l => l.StartsWith("Verification: all"));
    }

    [Fact]
    public async Task ConfiguredForm_GetsLibraryAndOnLoad_KeepsOtherHandlers_AndJoinsTheSolution()
    {
        var formId = Guid.NewGuid();
        _dataverse.AddForm(formId, "ogz_support_ticket", FormXml);
        var manifest = Manifest(new FormEntry
        {
            Entity = "ogz_support_ticket",
            FormId = formId.ToString(),
            Library = Name,
            Events = [new FormEventEntry { Event = "onload", Function = "Ogz.Forms.SupportTicket.onLoad" }],
        });
        var prompt = new ScriptedPrompt("DEV", "DEV");

        await Target(prompt, manifest: manifest).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        var formXml = (string)_dataverse.Find("systemform", formId)!["formxml"];
        Assert.Contains("functionName=\"Other.onLoad\"", formXml);
        Assert.Contains("name=\"ogz_/other.js\"", formXml);
        Assert.Contains("functionName=\"Ogz.Forms.SupportTicket.onLoad\"", formXml);
        Assert.True(_dataverse.IsMember(formId));
        Assert.Contains(_dataverse.PublishedXml, xml => xml.Contains("<entity>ogz_support_ticket</entity>"));
        Assert.Contains(prompt.Output, l => l.StartsWith("Verification: all"));

        // A second run finds nothing to do: no duplicate library or handler.
        var second = new ScriptedPrompt("DEV", "DEV");
        _dataverse.Writes.Clear();
        await Target(second, manifest: manifest).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);
        Assert.Empty(_dataverse.Writes);
    }

    [Fact]
    public async Task DecliningTheFormChange_LeavesTheFormUntouched()
    {
        var formId = Guid.NewGuid();
        _dataverse.AddForm(formId, "ogz_support_ticket", FormXml);
        var manifest = Manifest(new FormEntry
        {
            Entity = "ogz_support_ticket",
            FormId = formId.ToString(),
            Library = Name,
            Events = [new FormEventEntry { Event = "onload", Function = "Ogz.Forms.SupportTicket.onLoad" }],
        });

        await Target(new ScriptedPrompt("DEV", ""), manifest: manifest).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(FormXml, (string)_dataverse.Find("systemform", formId)!["formxml"]);
        Assert.False(_dataverse.IsMember(formId));
    }

    [Fact]
    public async Task DiscoveredForms_DryRun_AsksForTheForms_ShowsThemInThePlan_AndWritesNothing()
    {
        var (information, agent) = AddTicketForms();
        var prompt = new ScriptedPrompt("2");

        await Target(prompt, manifest: Manifest(DiscoveryEntry())).RunAsync(new DeploymentRunOptions(DryRunOnly: true), CancellationToken.None);

        Assert.Contains($"  1 - Agent ({agent})", prompt.Output);
        Assert.Contains($"  2 - Information ({information}) [default]", prompt.Output);
        Assert.Contains($"[Selected] form 'Information' ({information}) gets {Name}", prompt.Output);
        Assert.Contains($"[Update] form ogz_support_ticket/{information}: form 'Information'", prompt.Output);
        Assert.DoesNotContain(prompt.Output, l => l.Contains($"form ogz_support_ticket/{agent}"));
        Assert.Contains("Dry run: no changes made.", prompt.Output);
        Assert.Empty(_dataverse.Writes);
    }

    [Fact]
    public async Task DiscoveredForms_OnlyTheSelectedFormIsModified_AndASecondRunChangesNothing()
    {
        var (information, agent) = AddTicketForms();

        await Target(new ScriptedPrompt("2", "DEV", "DEV"), manifest: Manifest(DiscoveryEntry())).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains("Ogz.Forms.SupportTicket.onLoad", (string)_dataverse.Find("systemform", information)!["formxml"]);
        Assert.Contains("functionName=\"Other.onLoad\"", (string)_dataverse.Find("systemform", information)!["formxml"]);
        Assert.Equal(FormXml, (string)_dataverse.Find("systemform", agent)!["formxml"]);
        Assert.True(_dataverse.IsMember(information));
        Assert.False(_dataverse.IsMember(agent));

        _dataverse.Writes.Clear();
        var second = new ScriptedPrompt("2", "DEV", "DEV");
        await Target(second, manifest: Manifest(DiscoveryEntry())).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);
        Assert.Empty(_dataverse.Writes);
    }

    [Fact]
    public async Task DiscoveredForms_MultiSelection_RegistersOnEachSelectedForm()
    {
        var (information, agent) = AddTicketForms();

        await Target(new ScriptedPrompt("1,2", "DEV", "DEV"), manifest: Manifest(DiscoveryEntry())).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains("Ogz.Forms.SupportTicket.onLoad", (string)_dataverse.Find("systemform", information)!["formxml"]);
        Assert.Contains("Ogz.Forms.SupportTicket.onLoad", (string)_dataverse.Find("systemform", agent)!["formxml"]);
    }

    [Fact]
    public async Task DiscoveredForms_IgnoreOtherTablesTypesAndInactiveForms()
    {
        AddTicketForms();
        _dataverse.AddForm(Guid.NewGuid(), "account", FormXml, name: "Account");
        _dataverse.AddForm(Guid.NewGuid(), "ogz_support_ticket", FormXml, name: "Quick Create", type: 7);
        _dataverse.AddForm(Guid.NewGuid(), "ogz_support_ticket", FormXml, name: "Old", active: false);
        var prompt = new ScriptedPrompt("");

        await Target(prompt, manifest: Manifest(DiscoveryEntry())).RunAsync(new DeploymentRunOptions(DryRunOnly: true), CancellationToken.None);

        var listed = prompt.Output.Where(l => l.StartsWith("  ") && l.Contains(" - ")).ToList();
        Assert.Equal(2, listed.Count);
        Assert.DoesNotContain(listed, l => l.Contains("Account") || l.Contains("Quick Create") || l.Contains("Old"));
    }

    [Fact]
    public async Task NoSelection_DeploysTheWebResourceOnly()
    {
        var (information, _) = AddTicketForms();

        await Target(new ScriptedPrompt("", "DEV"), manifest: Manifest(DiscoveryEntry())).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.NotNull(_dataverse.FindWebResource(Name));
        Assert.Equal(FormXml, (string)_dataverse.Find("systemform", information)!["formxml"]);
    }

    // Regression for DEV: ogz_support_ticket was created in the solution, so the Information form is
    // in it through its table and has no solutioncomponent row. The form must not be "added" again,
    // and verification must not report it as missing.
    [Fact]
    public async Task FormOfATableCreatedInTheSolution_IsNotAddedAgain_VerifiesAndStaysIdempotent()
    {
        var formId = Guid.NewGuid();
        _dataverse.AddForm(formId, "ogz_support_ticket", FormXml);
        _dataverse.AddTableMember(_solutionId, "ogz_support_ticket");
        var prompt = new ScriptedPrompt("DEV", "DEV");

        await Target(prompt, manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains($"[Solution] Form ogz_support_ticket/{formId}: in {Solution} through its table ogz_support_ticket (all subcomponents)", prompt.Output);
        Assert.DoesNotContain(_dataverse.Writes, w => w.StartsWith("AddSolutionComponent 60"));
        Assert.Equal(0, _dataverse.MemberRows(formId));
        Assert.Contains("Ogz.Forms.SupportTicket.onLoad", (string)_dataverse.Find("systemform", formId)!["formxml"]);
        Assert.Contains(prompt.Output, l => l.StartsWith("Verification: all"));
        Assert.DoesNotContain(prompt.Output, l => l.StartsWith("VERIFICATION FAILED"));

        // Redeploying a changed build (the DEV case): only the Web Resource is updated and published.
        _repo.WriteFile(File, Script + "// v2");
        _dataverse.Writes.Clear();
        var second = new ScriptedPrompt("DEV", "DEV");
        await Target(second, manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(["Update webresource content", "PublishXml"], _dataverse.Writes);
        Assert.Contains(second.Output, l => l.StartsWith("Verification: all"));

        _dataverse.Writes.Clear();
        await Target(new ScriptedPrompt("DEV", "DEV"), manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);
        Assert.Empty(_dataverse.Writes);
    }

    [Fact]
    public async Task FormOfATableInTheSolutionWithoutSubcomponents_IsAddedOnce()
    {
        var formId = Guid.NewGuid();
        _dataverse.AddForm(formId, "ogz_support_ticket", FormXml);
        _dataverse.AddTableMember(_solutionId, "ogz_support_ticket", rootComponentBehavior: 1);
        var prompt = new ScriptedPrompt("DEV", "DEV");

        await Target(prompt, manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains($"AddSolutionComponent 60 {formId}", _dataverse.Writes);
        Assert.Equal(1, _dataverse.MemberRows(formId));
        Assert.Contains(prompt.Output, l => l.StartsWith("Verification: all"));

        _dataverse.Writes.Clear();
        await Target(new ScriptedPrompt("DEV", "DEV"), manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);
        Assert.Empty(_dataverse.Writes);
        Assert.Equal(1, _dataverse.MemberRows(formId));
    }

    [Fact]
    public async Task SavedButUnpublishedWebResource_IsPublishedWithoutAWrite()
    {
        var id = _dataverse.AddWebResource(Name, Encoding.UTF8.GetBytes("old"));
        _dataverse.AddMember(_solutionId, id, SolutionComponents.WebResource);
        _dataverse.SaveUnpublished(id, Convert.ToBase64String(Encoding.UTF8.GetBytes(Script)));
        var prompt = new ScriptedPrompt("DEV");

        await Target(prompt).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains($"[Publish] Web Resource {Name} (saved, but not published, 16 bytes)", prompt.Output);
        Assert.Equal(["PublishXml"], _dataverse.Writes);
        Assert.Contains($"<webresource>{id:B}</webresource>", _dataverse.PublishedXml.Single());
        Assert.Equal(Script, Encoding.UTF8.GetString(Convert.FromBase64String((string)_dataverse.Find("webresource", id)!["content"])));
        Assert.Contains(prompt.Output, l => l.StartsWith("Verification: all"));
    }

    [Fact]
    public async Task SavedButUnpublishedForm_IsPublishedByItsTable_WithoutRewritingTheFormXml()
    {
        var formId = Guid.NewGuid();
        _dataverse.AddWebResource(Name, Encoding.UTF8.GetBytes(Script));
        _dataverse.AddForm(formId, "ogz_support_ticket", FormXml);
        _dataverse.AddTableMember(_solutionId, "ogz_support_ticket");
        _dataverse.AddMember(_solutionId, _dataverse.FindWebResource(Name)!.Id, SolutionComponents.WebResource);
        var savedXml = Patched(FormXml);
        _dataverse.SaveUnpublished(formId, savedXml);
        var prompt = new ScriptedPrompt("DEV");

        await Target(prompt, manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains(prompt.Output, l => l.StartsWith($"[Publish] form ogz_support_ticket/{formId}: ") && l.EndsWith("but not published"));
        Assert.Equal(["PublishXml"], _dataverse.Writes);
        Assert.Contains("<entity>ogz_support_ticket</entity>", _dataverse.PublishedXml.Single());
        Assert.DoesNotContain("<webresource>", _dataverse.PublishedXml.Single());
        Assert.Equal(savedXml, (string)_dataverse.Find("systemform", formId)!["formxml"]);
        Assert.Contains(prompt.Output, l => l.StartsWith("Verification: all"));
    }

    [Fact]
    public async Task UnpublishedFormEdits_ArePatched_NotDiscarded()
    {
        var formId = Guid.NewGuid();
        _dataverse.AddForm(formId, "ogz_support_ticket", FormXml);
        _dataverse.AddTableMember(_solutionId, "ogz_support_ticket");
        _dataverse.SaveUnpublished(formId, FormXml.Replace("Other.onLoad", "Maker.onLoad"));

        await Target(new ScriptedPrompt("DEV", "DEV"), manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        var published = (string)_dataverse.Find("systemform", formId)!["formxml"];
        Assert.Contains("functionName=\"Maker.onLoad\"", published);
        Assert.Contains("functionName=\"Ogz.Forms.SupportTicket.onLoad\"", published);
    }

    [Fact]
    public async Task PendingPublishes_InADryRun_AreOnlyListed()
    {
        var id = _dataverse.AddWebResource(Name, Encoding.UTF8.GetBytes("old"));
        _dataverse.AddMember(_solutionId, id, SolutionComponents.WebResource);
        _dataverse.SaveUnpublished(id, Convert.ToBase64String(Encoding.UTF8.GetBytes(Script)));
        var prompt = new ScriptedPrompt();

        await Target(prompt).RunAsync(new DeploymentRunOptions(DryRunOnly: true), CancellationToken.None);

        Assert.Contains($"[Publish] Web Resource {Name} (saved, but not published, 16 bytes)", prompt.Output);
        Assert.Contains("Dry run: no changes made.", prompt.Output);
        Assert.Empty(_dataverse.Writes);
        Assert.Equal(0, prompt.Questions);
    }

    [Fact]
    public async Task FailedPublish_FailsVerification_AndIsRetriedByTheNextRun()
    {
        var formId = Guid.NewGuid();
        var id = _dataverse.AddWebResource(Name, Encoding.UTF8.GetBytes("old"));
        _dataverse.AddMember(_solutionId, id, SolutionComponents.WebResource);
        _dataverse.AddForm(formId, "ogz_support_ticket", FormXml);
        _dataverse.AddTableMember(_solutionId, "ogz_support_ticket");
        _dataverse.PublishFails = true;
        var prompt = new ScriptedPrompt("DEV", "DEV");

        await Target(prompt, manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Contains(prompt.Output, l => l.Contains("FAILED: publish: Publish failed."));
        Assert.Contains(prompt.Output, l => l.Contains($"Web Resource {Name}: the published content is not the deployed build (not published)."));
        Assert.Contains(prompt.Output, l => l.Contains($"Form ogz_support_ticket/{formId}: library or handlers are missing from the published form"));

        _dataverse.PublishFails = false;
        _dataverse.Writes.Clear();
        var retry = new ScriptedPrompt("DEV", "DEV");
        await Target(retry, manifest: Manifest(TicketForm(formId))).RunAsync(new DeploymentRunOptions(false), CancellationToken.None);

        Assert.Equal(["PublishXml"], _dataverse.Writes);
        Assert.Contains(retry.Output, l => l.StartsWith("Verification: all"));
    }

    private static FormEntry TicketForm(Guid formId) => new()
    {
        Entity = "ogz_support_ticket",
        FormId = formId.ToString(),
        Library = Name,
        Events = [new FormEventEntry { Event = "onload", Function = "Ogz.Forms.SupportTicket.onLoad" }],
    };

    private static string Patched(string formXml) =>
        FormXmlPatcher.Patch(formXml, Name, TicketForm(Guid.Empty).Events, Guid.NewGuid).UpdatedXml!;

    private (Guid Information, Guid Agent) AddTicketForms()
    {
        var information = Guid.NewGuid();
        var agent = Guid.NewGuid();
        _dataverse.AddForm(information, "ogz_support_ticket", FormXml, name: "Information", isDefault: true);
        _dataverse.AddForm(agent, "ogz_support_ticket", FormXml, name: "Agent");
        return (information, agent);
    }

    private static FormEntry DiscoveryEntry() => new()
    {
        Entity = "ogz_support_ticket",
        FormType = "main",
        Library = Name,
        Events = [new FormEventEntry { Event = "onload", Function = "Ogz.Forms.SupportTicket.onLoad" }],
    };

    private WebResourceDeploymentTarget Target(ScriptedPrompt prompt, InMemoryDataverse? dataverse = null, DeploymentManifest? manifest = null)
    {
        var session = new FakeSession(dataverse ?? _dataverse);
        return new WebResourceDeploymentTarget(
            new DeploymentSettings { SolutionUniqueName = Solution, PublisherPrefix = "ogz" },
            manifest ?? Manifest(),
            new ManifestValidator("ogz", _repo.Root),
            _build,
            new WebResourceRepository(session),
            new SolutionRepository(session),
            session,
            prompt);
    }

    private static DeploymentManifest Manifest(params FormEntry[] forms) => new()
    {
        WebResources = [new WebResourceEntry { Name = Name, File = File }],
        Forms = [.. forms],
    };

    private sealed class FakeBuild : IFrontendBuild
    {
        public bool Succeeds { get; set; } = true;
        public bool Ran { get; private set; }

        public Task<FrontendBuildResult> BuildAsync(DeploymentManifest manifest, CancellationToken cancellationToken)
        {
            Ran = true;
            return Task.FromResult(new FrontendBuildResult(Succeeds, [Succeeds ? "build passed" : "error TS2322"]));
        }
    }

    private sealed class ScriptedPrompt(params string[] answers) : IUserPrompt
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
