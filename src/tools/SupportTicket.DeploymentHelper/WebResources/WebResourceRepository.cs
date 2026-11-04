using System.Security;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SupportTicket.DeploymentHelper.Deployment;
using SupportTicket.DeploymentHelper.Forms;

namespace SupportTicket.DeploymentHelper.WebResources;

/// <summary>
/// Which copy of a Web Resource or form to read. An update is saved as an unpublished change that
/// users only get after publishing.
/// </summary>
public enum CustomizationState
{
    /// <summary>What users get: the last published content.</summary>
    Published,

    /// <summary>The latest saved content, including unpublished changes; what an update replaces.</summary>
    Latest,
}

/// <summary>Reads and writes webresource and systemform records. Contains no planning decisions.</summary>
public sealed class WebResourceRepository : IFormDiscovery
{
    private readonly IDataverseSession _session;

    public WebResourceRepository(IDataverseSession session)
    {
        _session = session;
    }

    private IOrganizationService Service => _session.Service;

    public Dictionary<string, ExistingWebResource> GetWebResources(IReadOnlyCollection<string> names, CustomizationState state)
    {
        var result = new Dictionary<string, ExistingWebResource>(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0) return result;

        var query = new QueryExpression("webresource") { ColumnSet = new ColumnSet("name", "displayname", "webresourcetype", "content", "ismanaged") };
        query.Criteria.AddCondition("name", ConditionOperator.In, names.Cast<object>().ToArray());

        foreach (var record in Retrieve(query, state))
        {
            var name = record.GetAttributeValue<string>("name");
            result[name] = new ExistingWebResource(
                record.Id,
                name,
                record.GetAttributeValue<OptionSetValue>("webresourcetype")?.Value ?? 0,
                record.GetAttributeValue<string>("content"),
                record.GetAttributeValue<bool>("ismanaged"),
                record.GetAttributeValue<string>("displayname"));
        }

        return result;
    }

    public Guid Create(WebResourcePlanItem item, string solutionUniqueName) =>
        SolutionComponents.CreateInSolution(Service, new Entity("webresource")
        {
            ["name"] = item.Local.Name,
            ["displayname"] = item.CreateDisplayName,
            ["webresourcetype"] = new OptionSetValue(item.Local.Type),
            ["content"] = item.ContentBase64,
        }, solutionUniqueName);

    /// <summary>Writes only the columns the plan found changed.</summary>
    public void Update(Guid id, WebResourcePlanItem item)
    {
        var update = new Entity("webresource", id);
        if (item.ContentChanged) update["content"] = item.ContentBase64;
        if (item.DisplayNameChanged) update["displayname"] = item.Local.DisplayName;
        Service.Update(update);
    }

    /// <summary>Active forms of a table and form type, ordered by name. Read-only.</summary>
    public IReadOnlyList<DiscoveredForm> FindForms(string entity, int formType)
    {
        var query = new QueryExpression("systemform") { ColumnSet = new ColumnSet("name", "objecttypecode", "isdefault", "iscustomizable") };
        query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, entity);
        query.Criteria.AddCondition("type", ConditionOperator.Equal, formType);
        query.Criteria.AddCondition("formactivationstate", ConditionOperator.Equal, 1);
        query.AddOrder("name", OrderType.Ascending);

        return Service.RetrieveMultiple(query).Entities
            .Select(f => new DiscoveredForm(
                f.Id,
                f.GetAttributeValue<string>("name") ?? string.Empty,
                f.GetAttributeValue<string>("objecttypecode") ?? entity,
                f.GetAttributeValue<bool>("isdefault"),
                f.GetAttributeValue<BooleanManagedProperty>("iscustomizable")?.Value ?? false))
            .OrderBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public SystemFormInfo? GetForm(Guid formId, CustomizationState state)
    {
        var query = new QueryExpression("systemform")
        {
            ColumnSet = new ColumnSet("name", "objecttypecode", "formxml", "iscustomizable", "ismanaged"),
        };
        query.Criteria.AddCondition("formid", ConditionOperator.Equal, formId);

        var form = Retrieve(query, state).SingleOrDefault();
        return form is null
            ? null
            : new SystemFormInfo(
                form.Id,
                form.GetAttributeValue<string>("name") ?? string.Empty,
                form.GetAttributeValue<string>("objecttypecode") ?? string.Empty,
                form.GetAttributeValue<string>("formxml") ?? string.Empty,
                form.GetAttributeValue<BooleanManagedProperty>("iscustomizable")?.Value ?? false);
    }

    /// <summary>RetrieveMultiple returns the published copy; RetrieveUnpublishedMultiple includes saved, unpublished changes.</summary>
    private IEnumerable<Entity> Retrieve(QueryExpression query, CustomizationState state) => state == CustomizationState.Published
        ? Service.RetrieveMultiple(query).Entities
        : ((RetrieveUnpublishedMultipleResponse)Service.Execute(new RetrieveUnpublishedMultipleRequest { Query = query })).EntityCollection.Entities;

    public void UpdateFormXml(Guid formId, string formXml) =>
        Service.Update(new Entity("systemform", formId) { ["formxml"] = formXml });

    /// <summary>Publishes only the given Web Resources and the tables of the given forms.</summary>
    public void Publish(IReadOnlyCollection<Guid> webResourceIds, IReadOnlyCollection<string> formEntities)
    {
        if (webResourceIds.Count == 0 && formEntities.Count == 0) return;

        var webResources = string.Concat(webResourceIds.Select(id => $"<webresource>{id:B}</webresource>"));
        var entities = string.Concat(formEntities.Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(e => $"<entity>{SecurityElement.Escape(e)}</entity>"));

        Service.Execute(new PublishXmlRequest
        {
            ParameterXml = $"<importexportxml><entities>{entities}</entities><webresources>{webResources}</webresources></importexportxml>",
        });
    }
}
