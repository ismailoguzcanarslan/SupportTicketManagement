using System.Text;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.WebResources;
using Xunit;

namespace SupportTicket.DeploymentHelperTests.WebResources;

public class WebResourcePlannerTests
{
    private const string Name = "ogz_/forms/account.js";
    private static readonly byte[] Script = Encoding.UTF8.GetBytes("var Ogz=Ogz||{};// ç ğ ü");

    [Fact]
    public void Missing_IsCreated()
    {
        var item = Assert.Single(Plan());

        Assert.Equal(PlannedAction.Create, item.Action);
        Assert.Equal(Script, Convert.FromBase64String(item.ContentBase64));
    }

    [Fact]
    public void SameBytes_AreUnchanged_SoRedeployIsIdempotent()
    {
        var existing = Existing(Convert.ToBase64String(Script));

        Assert.Equal(PlannedAction.Unchanged, Assert.Single(Plan(existing)).Action);
    }

    [Fact]
    public void DifferentBytes_AreUpdated()
    {
        var existing = Existing(Convert.ToBase64String(Encoding.UTF8.GetBytes("old")));

        var item = Assert.Single(Plan(existing));

        Assert.Equal(PlannedAction.Update, item.Action);
        Assert.Equal(existing.Id, item.ExistingId);
    }

    [Fact]
    public void ExistingWithoutContent_IsUpdated()
    {
        Assert.Equal(PlannedAction.Update, Assert.Single(Plan(Existing(null))).Action);
    }

    [Fact]
    public void ManagedOrDifferentType_IsBlocked()
    {
        Assert.Equal(PlannedAction.Blocked, Assert.Single(Plan(Existing(null) with { IsManaged = true })).Action);
        Assert.Equal(PlannedAction.Blocked, Assert.Single(Plan(Existing(null) with { Type = 1 })).Action);
    }

    [Fact]
    public void ChangedManifestDisplayName_UpdatesOnlyTheDisplayName()
    {
        var existing = Existing(Convert.ToBase64String(Script)) with { DisplayName = "Old name" };

        var item = Assert.Single(Plan(existing));

        Assert.Equal(PlannedAction.Update, item.Action);
        Assert.True(item.DisplayNameChanged);
        Assert.False(item.ContentChanged);
        Assert.Contains("display name 'Old name' -> 'Account form script'", item.Reason);
    }

    [Fact]
    public void ManifestWithoutDisplayName_NeverOverwritesTheExistingOne()
    {
        var existing = Existing(Convert.ToBase64String(Script)) with { DisplayName = "Set in Dataverse" };

        var item = Assert.Single(WebResourcePlanner.Plan(
            [new LocalWebResource(Name, null, 3, Script)],
            new Dictionary<string, ExistingWebResource> { [Name] = existing }));

        Assert.Equal(PlannedAction.Unchanged, item.Action);
    }

    [Fact]
    public void NewWebResourceWithoutDisplayName_UsesItsName()
    {
        var item = Assert.Single(WebResourcePlanner.Plan([new LocalWebResource(Name, null, 3, Script)], new Dictionary<string, ExistingWebResource>()));

        Assert.Equal(Name, item.CreateDisplayName);
    }

    [Fact]
    public void TypeIsDerivedFromExtension()
    {
        Assert.Equal(3, WebResourceType.FromFileName("ogz_/forms/account.js"));
        Assert.Equal(2, WebResourceType.FromFileName("ogz_/styles/site.CSS"));
        Assert.Null(WebResourceType.FromFileName("ogz_/forms/account.ts"));
    }

    [Fact]
    public void SavedButUnpublishedContent_OnlyNeedsPublishing()
    {
        var saved = Existing(Convert.ToBase64String(Script));
        var published = saved with { ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes("old")) };

        var item = Assert.Single(PlanWithPublished(saved, published));

        Assert.Equal(PlannedAction.Unchanged, item.Action);
        Assert.True(item.PublishPending);
        Assert.Equal("Publish", item.DisplayAction);
        Assert.False(item.ContentChanged);
    }

    [Fact]
    public void PublishedContentMatchingTheBuild_NeedsNoPublish()
    {
        var saved = Existing(Convert.ToBase64String(Script));

        var item = Assert.Single(PlanWithPublished(saved, saved));

        Assert.False(item.PublishPending);
        Assert.Equal("Unchanged", item.DisplayAction);
    }

    [Fact]
    public void ChangedSavedContent_IsUpdated_WhateverIsPublished()
    {
        var saved = Existing(Convert.ToBase64String(Encoding.UTF8.GetBytes("unpublished edit")));

        var item = Assert.Single(PlanWithPublished(saved, saved with { ContentBase64 = Convert.ToBase64String(Script) }));

        Assert.Equal(PlannedAction.Update, item.Action);
        Assert.False(item.PublishPending);
    }

    private static IReadOnlyList<WebResourcePlanItem> PlanWithPublished(ExistingWebResource saved, ExistingWebResource published) =>
        WebResourcePlanner.Plan(
            [new LocalWebResource(Name, "Account form script", 3, Script)],
            new Dictionary<string, ExistingWebResource> { [Name] = saved },
            new Dictionary<string, ExistingWebResource> { [Name] = published });

    private static ExistingWebResource Existing(string? content) => new(Guid.NewGuid(), Name, 3, content, false, "Account form script");

    private static IReadOnlyList<WebResourcePlanItem> Plan(params ExistingWebResource[] existing) =>
        WebResourcePlanner.Plan(
            [new LocalWebResource(Name, "Account form script", 3, Script)],
            existing.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase));
}
