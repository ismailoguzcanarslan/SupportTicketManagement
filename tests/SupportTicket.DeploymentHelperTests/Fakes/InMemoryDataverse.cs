using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelperTests.Fakes;

/// <summary>
/// Small in-memory stand-in for the Dataverse calls the Web Resource pipeline makes: the solution
/// (with its publisher), webresource, systemform and solutioncomponent queries, plus the writes.
/// Every write is recorded so tests can prove what did, or did not, happen.
/// Like Dataverse, an update of webresource content or systemform formxml is saved unpublished:
/// RetrieveMultiple keeps returning the published value until PublishXml names the Web Resource or
/// the form's table; RetrieveUnpublishedMultiple returns the saved value.
/// </summary>
public sealed class InMemoryDataverse : IOrganizationService
{
    private static readonly Dictionary<string, string> StagedColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        ["webresource"] = "content",
        ["systemform"] = "formxml",
    };

    private readonly Dictionary<string, List<Entity>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Guid, object> _unpublished = [];
    private readonly Dictionary<string, Guid> _tableIds = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Writes { get; } = [];

    public List<string> PublishedXml { get; } = [];

    public int Calls { get; private set; }

    /// <summary>When false, Create ignores SolutionUniqueName (to simulate a component that did not join the solution).</summary>
    public bool CreateJoinsSolution { get; set; } = true;

    /// <summary>When true, PublishXml throws and publishes nothing.</summary>
    public bool PublishFails { get; set; }

    public Guid AddSolution(string uniqueName, bool isManaged = false, string publisherPrefix = "ogz")
    {
        var solution = new Entity("solution", Guid.NewGuid())
        {
            ["uniquename"] = uniqueName,
            ["friendlyname"] = "Support Ticket Management",
            ["ismanaged"] = isManaged,
            ["publisher.customizationprefix"] = new AliasedValue("publisher", "customizationprefix", publisherPrefix),
        };
        Table("solution").Add(solution);
        return solution.Id;
    }

    public Guid AddWebResource(string name, byte[] content, string? displayName = null)
    {
        var record = new Entity("webresource", Guid.NewGuid())
        {
            ["name"] = name,
            ["displayname"] = displayName,
            ["webresourcetype"] = new OptionSetValue(3),
            ["content"] = Convert.ToBase64String(content),
            ["ismanaged"] = false,
        };
        Table("webresource").Add(record);
        return record.Id;
    }

    public void AddForm(Guid formId, string entity, string formXml, string name = "Information", int type = 2, bool active = true, bool isDefault = false)
    {
        Table("systemform").Add(new Entity("systemform", formId)
        {
            ["name"] = name,
            ["objecttypecode"] = entity,
            ["type"] = new OptionSetValue(type),
            ["formactivationstate"] = new OptionSetValue(active ? 1 : 0),
            ["isdefault"] = isDefault,
            ["formxml"] = formXml,
            ["iscustomizable"] = new BooleanManagedProperty(true),
        });
    }

    /// <param name="rootComponentBehavior">0 = include subcomponents, 1 = do not include, 2 = include as shell.</param>
    public void AddMember(Guid solutionId, Guid objectId, int componentType, int rootComponentBehavior = 0) =>
        Table("solutioncomponent").Add(new Entity("solutioncomponent", Guid.NewGuid())
        {
            ["solutionid"] = new EntityReference("solution", solutionId),
            ["objectid"] = objectId,
            ["componenttype"] = new OptionSetValue(componentType),
            ["rootcomponentbehavior"] = new OptionSetValue(rootComponentBehavior),
        });

    /// <summary>Adds a table to the solution, as creating the table inside the solution does.</summary>
    public void AddTableMember(Guid solutionId, string entity, int rootComponentBehavior = 0) =>
        AddMember(solutionId, TableId(entity), SolutionComponents.Entity, rootComponentBehavior);

    /// <summary>The table's MetadataId (stable per logical name).</summary>
    public Guid TableId(string entity)
    {
        if (!_tableIds.TryGetValue(entity, out var id)) _tableIds[entity] = id = Guid.NewGuid();
        return id;
    }

    /// <summary>Saves a value without publishing it, as a maker portal edit or a failed publish leaves it.</summary>
    public void SaveUnpublished(Guid id, object value) => _unpublished[id] = value;

    /// <summary>The saved, unpublished value of a Web Resource's content or a form's FormXML, if any.</summary>
    public object? Unpublished(Guid id) => _unpublished.GetValueOrDefault(id);

    /// <summary>How many solutioncomponent rows the object has (0 when only its table includes it).</summary>
    public int MemberRows(Guid objectId) => Table("solutioncomponent").Count(c => (Guid)c["objectid"] == objectId);

    public Entity? Find(string table, Guid id) => Table(table).SingleOrDefault(e => e.Id == id);

    public Entity? FindWebResource(string name) => Table("webresource").SingleOrDefault(e => (string)e["name"] == name);

    public bool IsMember(Guid objectId) => Table("solutioncomponent").Any(c => (Guid)c["objectid"] == objectId);

    public EntityCollection RetrieveMultiple(QueryBase query)
    {
        Calls++;
        return Query((QueryExpression)query, unpublished: false);
    }

    public OrganizationResponse Execute(OrganizationRequest request)
    {
        Calls++;
        switch (request)
        {
            case AddSolutionComponentRequest add:
                Writes.Add($"AddSolutionComponent {add.ComponentType} {add.ComponentId}");
                // Like Dataverse: a form whose table is in the solution with all subcomponents gets no row.
                if (!(add.ComponentType == SolutionComponents.SystemForm && IsIncludedByItsTable(add.SolutionUniqueName, add.ComponentId)))
                    AddMember(SolutionId(add.SolutionUniqueName), add.ComponentId, add.ComponentType);
                return new AddSolutionComponentResponse();
            case PublishXmlRequest publish:
                Writes.Add("PublishXml");
                if (PublishFails) throw new InvalidOperationException("Publish failed.");
                PublishedXml.Add(publish.ParameterXml);
                Publish(publish.ParameterXml);
                return new PublishXmlResponse();
            case RetrieveUnpublishedMultipleRequest unpublished:
                return new RetrieveUnpublishedMultipleResponse { Results = { ["EntityCollection"] = Query((QueryExpression)unpublished.Query, unpublished: true) } };
            case Microsoft.Xrm.Sdk.Messages.RetrieveEntityRequest metadata:
                return new Microsoft.Xrm.Sdk.Messages.RetrieveEntityResponse
                {
                    Results = { ["EntityMetadata"] = new Microsoft.Xrm.Sdk.Metadata.EntityMetadata { LogicalName = metadata.LogicalName, MetadataId = TableId(metadata.LogicalName) } },
                };
        }

        if (request.RequestName == "Create")
        {
            var target = (Entity)request["Target"];
            var id = Create(target);
            if (CreateJoinsSolution && request.Parameters.TryGetValue("SolutionUniqueName", out var solution))
                AddMember(SolutionId((string)solution), id, target.LogicalName == "webresource" ? SolutionComponents.WebResource : 0);
            return new OrganizationResponse { Results = { ["id"] = id } };
        }

        throw new NotSupportedException($"InMemoryDataverse does not support {request.RequestName}.");
    }

    public Guid Create(Entity entity)
    {
        Calls++;
        Writes.Add($"Create {entity.LogicalName}");
        var copy = Copy(entity);
        copy.Id = copy.Id == Guid.Empty ? Guid.NewGuid() : copy.Id;
        copy["ismanaged"] = false;
        Table(entity.LogicalName).Add(copy);
        return copy.Id;
    }

    public void Update(Entity entity)
    {
        Calls++;
        Writes.Add($"Update {entity.LogicalName} {string.Join(",", entity.Attributes.Keys.Order())}");
        var row = Find(entity.LogicalName, entity.Id) ?? throw new InvalidOperationException($"{entity.LogicalName} {entity.Id} not found");
        foreach (var attribute in entity.Attributes)
        {
            if (StagedColumns.TryGetValue(entity.LogicalName, out var staged) && attribute.Key == staged) _unpublished[entity.Id] = attribute.Value;
            else row[attribute.Key] = attribute.Value;
        }
    }

    private EntityCollection Query(QueryExpression expression, bool unpublished)
    {
        var rows = Table(expression.EntityName)
            .Select(row =>
            {
                var copy = Copy(row);
                if (unpublished && StagedColumns.TryGetValue(row.LogicalName, out var staged) && _unpublished.TryGetValue(row.Id, out var value))
                    copy[staged] = value;
                return copy;
            })
            .Where(row => expression.Criteria.Conditions.All(c => Matches(row, c, expression.EntityName)));
        return new EntityCollection(rows.ToList());
    }

    /// <summary>Publishes the listed Web Resources and the forms of the listed tables; nothing else.</summary>
    private void Publish(string parameterXml)
    {
        var xml = System.Xml.Linq.XElement.Parse(parameterXml);
        var webResourceIds = xml.Descendants("webresource").Select(w => Guid.Parse(w.Value)).ToHashSet();
        var entities = xml.Descendants("entity").Select(e => e.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var row in Table("webresource").Where(r => webResourceIds.Contains(r.Id) && _unpublished.ContainsKey(r.Id)).ToList())
            row["content"] = _unpublished.Remove(row.Id, out var content) ? content : row["content"];
        foreach (var row in Table("systemform").Where(r => entities.Contains((string)r["objecttypecode"]) && _unpublished.ContainsKey(r.Id)).ToList())
            row["formxml"] = _unpublished.Remove(row.Id, out var formXml) ? formXml : row["formxml"];
    }

    private bool IsIncludedByItsTable(string solutionUniqueName, Guid formId)
    {
        var solutionId = SolutionId(solutionUniqueName);
        var entity = (string)Find("systemform", formId)!["objecttypecode"];
        return Table("solutioncomponent").Any(c =>
            ((EntityReference)c["solutionid"]).Id == solutionId
            && (Guid)c["objectid"] == TableId(entity)
            && ((OptionSetValue)c["rootcomponentbehavior"]).Value == 0);
    }

    public Entity Retrieve(string entityName, Guid id, ColumnSet columnSet) => throw new NotSupportedException();

    public void Delete(string entityName, Guid id) => throw new NotSupportedException("Deployments must never delete.");

    public void Associate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();

    public void Disassociate(string entityName, Guid entityId, Relationship relationship, EntityReferenceCollection relatedEntities) => throw new NotSupportedException();

    private Guid SolutionId(string uniqueName) => Table("solution").Single(s => (string)s["uniquename"] == uniqueName).Id;

    private List<Entity> Table(string name)
    {
        if (!_tables.TryGetValue(name, out var rows)) _tables[name] = rows = [];
        return rows;
    }

    private static bool Matches(Entity row, ConditionExpression condition, string table)
    {
        var column = condition.AttributeName == $"{table}id" || condition.AttributeName == "formid" ? null : condition.AttributeName;
        object? value = column is null ? row.Id : row.Contains(column) ? row[column] : null;
        if (value is EntityReference reference) value = reference.Id;
        if (value is OptionSetValue option) value = option.Value;

        return condition.Operator switch
        {
            ConditionOperator.Equal => Equals(value, condition.Values[0]),
            ConditionOperator.In => condition.Values.Any(v => Equals(value, v)),
            _ => throw new NotSupportedException($"Operator {condition.Operator}"),
        };
    }

    private static Entity Copy(Entity source)
    {
        var copy = new Entity(source.LogicalName, source.Id);
        foreach (var attribute in source.Attributes) copy[attribute.Key] = attribute.Value;
        return copy;
    }
}

public sealed class FakeSession(IOrganizationService service) : IDataverseSession
{
    public string EnvironmentName => "DEV";

    public string Description => "Contoso DEV (https://contoso-dev.crm4.dynamics.com)";

    public IOrganizationService Service { get; } = service;
}
