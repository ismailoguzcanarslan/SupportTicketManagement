using System.Xml.Linq;
using SupportTicket.DeploymentHelper.Configuration;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Forms;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.Forms;

public class FormXmlPatcherTests
{
    private const string Library = "ogz_/forms/account.js";

    private const string FormWithoutScripts =
        """<form><tabs><tab name="SUMMARY_TAB"><labels><label description="Summary" languagecode="1033" /></labels></tab></tabs><header /><footer /><events><event name="onload" application="true" active="true"><InternalHandlers><Handler functionName="Mscrm.Form_onload" libraryName="Form_onload" handlerUniqueId="{11111111-1111-1111-1111-111111111111}" enabled="true" /></InternalHandlers></event></events><Navigation /></form>""";

    private static readonly FormEventEntry OnLoad = new() { Event = "onload", Function = "Ogz.Forms.Account.onLoad" };

    [Fact]
    public void AddsLibraryAndCustomOnLoadHandler()
    {
        var result = Patch(FormWithoutScripts, OnLoad);

        Assert.True(result.Succeeded);
        Assert.Equal(["add form library ogz_/forms/account.js", "add onload handler Ogz.Forms.Account.onLoad"], result.Changes);

        var form = XElement.Parse(result.UpdatedXml!);
        var library = Assert.Single(form.Element("formLibraries")!.Elements("Library"));
        Assert.Equal(Library, (string?)library.Attribute("name"));

        var custom = form.Element("events")!.Elements("event").Single(e => (string?)e.Attribute("application") == "false");
        var handler = Assert.Single(custom.Element("Handlers")!.Elements("Handler"));
        Assert.Equal("Ogz.Forms.Account.onLoad", (string?)handler.Attribute("functionName"));
        Assert.Equal("true", (string?)handler.Attribute("passExecutionContext"));
    }

    [Fact]
    public void PreservesSystemHandlersAndUnrelatedContent()
    {
        var form = XElement.Parse(Patch(FormWithoutScripts, OnLoad).UpdatedXml!);

        var system = form.Element("events")!.Elements("event").Single(e => (string?)e.Attribute("application") == "true");
        Assert.Equal("Mscrm.Form_onload", (string?)system.Element("InternalHandlers")!.Element("Handler")!.Attribute("functionName"));
        Assert.Equal("SUMMARY_TAB", (string?)form.Element("tabs")!.Element("tab")!.Attribute("name"));
        Assert.NotNull(form.Element("Navigation"));
    }

    [Fact]
    public void InsertsMissingSectionsInFormXmlOrder()
    {
        var form = XElement.Parse(Patch("<form><tabs /><header /><footer /><Navigation /></form>", OnLoad).UpdatedXml!);

        Assert.Equal(["tabs", "header", "footer", "events", "formLibraries", "Navigation"],
            form.Elements().Select(e => e.Name.LocalName));
    }

    [Fact]
    public void SecondPatch_HasNoChanges_SoDuplicatesAreNeverAdded()
    {
        var once = Patch(FormWithoutScripts, OnLoad).UpdatedXml!;

        var twice = Patch(once, OnLoad);

        Assert.True(twice.Succeeded);
        Assert.False(twice.HasChanges);
        Assert.Equal(once, twice.UpdatedXml);
    }

    [Fact]
    public void ExistingLibraryMatchesCaseInsensitively()
    {
        var xml = FormWithoutScripts.Replace("<Navigation />",
            """<formLibraries><Library name="OGZ_/forms/Account.js" libraryUniqueId="{22222222-2222-2222-2222-222222222222}" /></formLibraries><Navigation />""");

        var result = Patch(xml, OnLoad);

        Assert.DoesNotContain(result.Changes, c => c.StartsWith("add form library"));
    }

    [Fact]
    public void AddsOnSaveNextToExistingOnLoad()
    {
        var once = Patch(FormWithoutScripts, OnLoad).UpdatedXml!;

        var result = Patch(once, OnLoad, new FormEventEntry { Event = "onsave", Function = "Ogz.Forms.Account.onSave" });

        Assert.Equal(["add onsave handler Ogz.Forms.Account.onSave"], result.Changes);
    }

    [Theory]
    [InlineData("<form><events")]
    [InlineData("<entity />")]
    [InlineData("<form><events /><events /></form>")]
    public void UnexpectedFormXml_FailsInsteadOfGuessing(string xml)
    {
        var result = Patch(xml, OnLoad);

        Assert.False(result.Succeeded);
        Assert.Null(result.UpdatedXml);
    }

    [Fact]
    public void Planner_BlocksWrongTableAndNonCustomizableForms()
    {
        var entry = new FormEntry { Entity = "account", FormId = Guid.NewGuid().ToString(), Library = Library, Events = [OnLoad] };

        Assert.Equal(PlannedAction.Blocked, FormEventPlanner.Plan(entry, null, Guid.NewGuid).Action);
        Assert.Equal(PlannedAction.Blocked, FormEventPlanner.Plan(entry, Form("contact", true), Guid.NewGuid).Action);
        Assert.Equal(PlannedAction.Blocked, FormEventPlanner.Plan(entry, Form("account", false), Guid.NewGuid).Action);
        Assert.Equal(PlannedAction.Update, FormEventPlanner.Plan(entry, Form("account", true), Guid.NewGuid).Action);
    }

    private static SystemFormInfo Form(string entity, bool customizable) =>
        new(Guid.NewGuid(), "Account", entity, FormWithoutScripts, customizable);

    private static FormPatchResult Patch(string xml, params FormEventEntry[] events) =>
        FormXmlPatcher.Patch(xml, Library, events, Guid.NewGuid);
}
