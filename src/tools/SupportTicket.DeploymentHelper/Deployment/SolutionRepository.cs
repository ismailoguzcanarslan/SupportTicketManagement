using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace SupportTicket.DeploymentHelper.Deployment;

/// <summary>Reads the target solution and its members, and adds existing components to it.</summary>
public sealed class SolutionRepository
{
    private readonly IDataverseSession _session;
    private readonly Dictionary<string, EntityMetadata> _metadata = new(StringComparer.OrdinalIgnoreCase);

    public SolutionRepository(IDataverseSession session)
    {
        _session = session;
    }

    private IOrganizationService Service => _session.Service;

    public SolutionInfo? GetSolution(string uniqueName)
    {
        var query = new QueryExpression("solution") { ColumnSet = new ColumnSet("uniquename", "friendlyname", "ismanaged") };
        query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, uniqueName);
        var publisher = query.AddLink("publisher", "publisherid", "publisherid");
        publisher.EntityAlias = "publisher";
        publisher.Columns = new ColumnSet("customizationprefix");

        var solution = Service.RetrieveMultiple(query).Entities.SingleOrDefault();
        return solution is null
            ? null
            : new SolutionInfo(
                solution.Id,
                solution.GetAttributeValue<string>("uniquename"),
                solution.GetAttributeValue<string>("friendlyname") ?? string.Empty,
                solution.GetAttributeValue<bool>("ismanaged"),
                solution.GetAttributeValue<AliasedValue>("publisher.customizationprefix")?.Value as string ?? string.Empty);
    }

    /// <summary>Which of the given components are already in the solution.</summary>
    public Dictionary<Guid, SolutionMember> GetMembers(Guid solutionId, IEnumerable<Guid> objectIds)
    {
        var ids = objectIds.Distinct().ToList();
        if (ids.Count == 0) return new Dictionary<Guid, SolutionMember>();

        var query = new QueryExpression("solutioncomponent") { ColumnSet = new ColumnSet("objectid", "componenttype", "rootcomponentbehavior") };
        query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
        query.Criteria.AddCondition("objectid", ConditionOperator.In, ids.Cast<object>().ToArray());

        return Service.RetrieveMultiple(query).Entities
            .Select(c => new SolutionMember(
                c.GetAttributeValue<Guid>("objectid"),
                c.GetAttributeValue<OptionSetValue>("componenttype")?.Value ?? 0,
                c.GetAttributeValue<OptionSetValue>("rootcomponentbehavior")?.Value ?? 0))
            .GroupBy(m => m.ObjectId)
            .ToDictionary(g => g.Key, g => g.First());
    }

    /// <summary>
    /// The solution's rows for the given tables (component type 1, keyed by logical name). A table
    /// that is not in the solution is missing from the result.
    /// </summary>
    public Dictionary<string, SolutionMember> GetTableMembers(Guid solutionId, IEnumerable<string> entityLogicalNames)
    {
        var tableIds = entityLogicalNames
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(name => GetMetadata(name).MetadataId!.Value, name => name);
        var result = new Dictionary<string, SolutionMember>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in GetMembers(solutionId, tableIds.Keys).Values.Where(m => m.ComponentType == SolutionComponents.Entity))
            result[tableIds[member.ObjectId]] = member;

        return result;
    }

    /// <summary>
    /// Component type of a table without a fixed solution component type (e.g. pluginpackage): its
    /// object type code, read from metadata instead of hardcoding an environment-specific number.
    /// </summary>
    public int GetComponentType(string entityLogicalName) =>
        GetMetadata(entityLogicalName).ObjectTypeCode
        ?? throw new InvalidOperationException($"Table {entityLogicalName} has no object type code.");

    private EntityMetadata GetMetadata(string entityLogicalName)
    {
        if (_metadata.TryGetValue(entityLogicalName, out var cached)) return cached;

        var response = (RetrieveEntityResponse)Service.Execute(new RetrieveEntityRequest
        {
            LogicalName = entityLogicalName,
            EntityFilters = EntityFilters.Entity,
        });
        return _metadata[entityLogicalName] = response.EntityMetadata;
    }

    /// <summary>Adds an existing component with its subcomponents (e.g. a step with its images).</summary>
    public void Add(SolutionAssociation association, string solutionUniqueName)
    {
        if (association.Action != AssociationAction.Add || association.ObjectId is not { } objectId)
            throw new InvalidOperationException($"{association.Label} is not planned to be added.");

        Service.Execute(new AddSolutionComponentRequest
        {
            ComponentId = objectId,
            ComponentType = association.ComponentType,
            SolutionUniqueName = solutionUniqueName,
            AddRequiredComponents = false,
            DoNotIncludeSubcomponents = false,
        });
    }
}
