using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using SupportTicket.DeploymentHelper.Deployment;

namespace SupportTicket.DeploymentHelper.Plugins;

/// <summary>Reads and writes plug-in registration tables. Contains no planning decisions.</summary>
public sealed class PluginRegistrationRepository
{
    private readonly IDataverseSession _session;

    public PluginRegistrationRepository(IDataverseSession session)
    {
        _session = session;
    }

    private IOrganizationService Service => _session.Service;

    public RegistrationSnapshot LoadSnapshot(string packageId, IReadOnlyCollection<DeclaredStep> declared)
    {
        var package = GetPackage(packageId);
        var pluginTypes = package is null ? new Dictionary<string, Guid>(StringComparer.Ordinal) : GetPluginTypes(package.Id);
        var messages = GetMessages(declared.Select(s => s.MessageName).Distinct(StringComparer.OrdinalIgnoreCase).ToList());

        return new RegistrationSnapshot
        {
            Package = package,
            PluginTypes = pluginTypes,
            Messages = messages,
            Filters = GetFilters(messages.Values.Select(m => m.Id).ToList(), messages),
            Steps = GetSteps(pluginTypes.Values.ToList()),
            ForeignSteps = GetForeignSteps(declared.Select(s => s.PluginTypeName).Distinct(StringComparer.Ordinal).ToList(), package?.Id),
        };
    }

    public void CreatePackage(string packageId, PluginPackageFile file, string solutionUniqueName)
    {
        // name, uniquename and version are fixed for the lifetime of the package in Dataverse.
        var package = new Entity("pluginpackage")
        {
            ["name"] = packageId,
            ["uniquename"] = packageId,
            ["version"] = file.Version,
            ["content"] = Convert.ToBase64String(file.Content),
        };
        SolutionComponents.CreateInSolution(Service, package, solutionUniqueName);
    }

    /// <summary>Replaces the package content only; name and version cannot be changed after creation.</summary>
    public void UpdatePackage(Guid packageId, PluginPackageFile file)
    {
        Service.Update(new Entity("pluginpackage", packageId)
        {
            ["content"] = Convert.ToBase64String(file.Content),
        });
    }

    public Guid CreateStep(DeclaredStep step, Guid pluginTypeId, Guid messageId, Guid? filterId, string solutionUniqueName)
    {
        var entity = new Entity("sdkmessageprocessingstep")
        {
            ["eventhandler"] = new EntityReference("plugintype", pluginTypeId),
            ["sdkmessageid"] = new EntityReference("sdkmessage", messageId),
            ["supporteddeployment"] = new OptionSetValue(0), // server only
        };
        if (filterId is { } filter) entity["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filter);
        ApplyStepFields(entity, step);
        entity["stage"] = new OptionSetValue(step.Stage);

        return SolutionComponents.CreateInSolution(Service, entity, solutionUniqueName);
    }

    public void UpdateStep(Guid stepId, DeclaredStep step)
    {
        var entity = new Entity("sdkmessageprocessingstep", stepId);
        ApplyStepFields(entity, step);
        Service.Update(entity);
    }

    public void CreateImage(Guid stepId, DeclaredStep step, DeclaredImage image, string solutionUniqueName)
    {
        SolutionComponents.CreateInSolution(Service, new Entity("sdkmessageprocessingstepimage")
        {
            ["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId),
            ["imagetype"] = new OptionSetValue(image.ImageType),
            ["name"] = image.Alias,
            ["entityalias"] = image.Alias,
            ["attributes"] = image.Attributes,
            ["messagepropertyname"] = StepValidator.ImageMessageProperty(step.MessageName),
        }, solutionUniqueName);
    }

    public void UpdateImage(Guid imageId, DeclaredImage image)
    {
        Service.Update(new Entity("sdkmessageprocessingstepimage", imageId) { ["attributes"] = image.Attributes });
    }

    public void LinkCustomApi(Guid customApiId, Guid pluginTypeId)
    {
        Service.Update(new Entity("customapi", customApiId) { ["plugintypeid"] = new EntityReference("plugintype", pluginTypeId) });
    }

    private static void ApplyStepFields(Entity entity, DeclaredStep step)
    {
        entity["name"] = step.Name;
        entity["description"] = step.Description;
        entity["mode"] = new OptionSetValue(step.Async ? 1 : 0);
        entity["rank"] = step.ExecutionOrder;
        entity["filteringattributes"] = step.FilteringAttributes;
        entity["asyncautodelete"] = step.Async && step.DeleteAsyncOperationIfSuccessful;
        // impersonatinguserid is deliberately not managed: user ids differ per environment.
    }

    private ExistingPackage? GetPackage(string packageId)
    {
        var query = new QueryExpression("pluginpackage") { ColumnSet = new ColumnSet("pluginpackageid", "version", "content") };
        query.Criteria.AddCondition("uniquename", ConditionOperator.Equal, packageId);

        var record = Service.RetrieveMultiple(query).Entities.SingleOrDefault();
        if (record is null) return null;

        var content = record.GetAttributeValue<string>("content");
        return new ExistingPackage(record.Id, record.GetAttributeValue<string>("version") ?? string.Empty,
            string.IsNullOrEmpty(content) ? null : Convert.FromBase64String(content));
    }

    private Dictionary<string, Guid> GetPluginTypes(Guid packageId)
    {
        var query = new QueryExpression("plugintype") { ColumnSet = new ColumnSet("typename") };
        var assembly = query.AddLink("pluginassembly", "pluginassemblyid", "pluginassemblyid");
        assembly.LinkCriteria.AddCondition("packageid", ConditionOperator.Equal, packageId);

        return Service.RetrieveMultiple(query).Entities
            .ToDictionary(e => e.GetAttributeValue<string>("typename"), e => e.Id, StringComparer.Ordinal);
    }

    /// <summary>Steps of plug-in types with the given class names whose assembly is not part of the package.</summary>
    private List<ForeignStep> GetForeignSteps(IReadOnlyCollection<string> typeNames, Guid? packageId)
    {
        if (typeNames.Count == 0) return [];

        var query = new QueryExpression("sdkmessageprocessingstep") { ColumnSet = new ColumnSet("name") };
        var type = query.AddLink("plugintype", "plugintypeid", "plugintypeid");
        type.EntityAlias = "type";
        type.Columns = new ColumnSet("typename");
        type.LinkCriteria.AddCondition("typename", ConditionOperator.In, typeNames.Cast<object>().ToArray());
        var assembly = type.AddLink("pluginassembly", "pluginassemblyid", "pluginassemblyid");
        assembly.EntityAlias = "assembly";
        assembly.Columns = new ColumnSet("name", "packageid");

        // Filtered in memory: assemblies without a package have packageid = null.
        return Service.RetrieveMultiple(query).Entities
            .Where(s => (s.GetAttributeValue<AliasedValue>("assembly.packageid")?.Value as EntityReference)?.Id != packageId || packageId is null)
            .Select(s => new ForeignStep(
                (string)s.GetAttributeValue<AliasedValue>("type.typename").Value,
                s.GetAttributeValue<AliasedValue>("assembly.name")?.Value as string ?? "unknown assembly",
                s.GetAttributeValue<string>("name") ?? string.Empty))
            .ToList();
    }

    private Dictionary<string, SdkMessageInfo> GetMessages(IReadOnlyCollection<string> names)
    {
        var result = new Dictionary<string, SdkMessageInfo>(StringComparer.OrdinalIgnoreCase);
        if (names.Count == 0) return result;

        var messageQuery = new QueryExpression("sdkmessage") { ColumnSet = new ColumnSet("name") };
        messageQuery.Criteria.AddCondition("name", ConditionOperator.In, names.Cast<object>().ToArray());

        var apiQuery = new QueryExpression("customapi")
        {
            ColumnSet = new ColumnSet("uniquename", "plugintypeid", "allowedcustomprocessingsteptype", "boundentitylogicalname"),
        };
        apiQuery.Criteria.AddCondition("uniquename", ConditionOperator.In, names.Cast<object>().ToArray());
        var apis = Service.RetrieveMultiple(apiQuery).Entities.ToDictionary(
            e => e.GetAttributeValue<string>("uniquename"),
            e => new CustomApiInfo(
                e.Id,
                e.GetAttributeValue<string>("uniquename"),
                e.GetAttributeValue<EntityReference>("plugintypeid")?.Id,
                e.GetAttributeValue<OptionSetValue>("allowedcustomprocessingsteptype")?.Value ?? 0,
                e.GetAttributeValue<string>("boundentitylogicalname")),
            StringComparer.OrdinalIgnoreCase);

        foreach (var message in Service.RetrieveMultiple(messageQuery).Entities)
        {
            var name = message.GetAttributeValue<string>("name");
            result[name] = new SdkMessageInfo(message.Id, name, apis.GetValueOrDefault(name));
        }

        return result;
    }

    private Dictionary<string, Guid> GetFilters(IReadOnlyCollection<Guid> messageIds, IReadOnlyDictionary<string, SdkMessageInfo> messages)
    {
        var result = new Dictionary<string, Guid>(StringComparer.Ordinal);
        if (messageIds.Count == 0) return result;

        var namesById = messages.Values.ToDictionary(m => m.Id, m => m.Name);
        var query = new QueryExpression("sdkmessagefilter") { ColumnSet = new ColumnSet("sdkmessageid", "primaryobjecttypecode") };
        query.Criteria.AddCondition("sdkmessageid", ConditionOperator.In, messageIds.Cast<object>().ToArray());

        foreach (var filter in Service.RetrieveMultiple(query).Entities)
        {
            var messageId = filter.GetAttributeValue<EntityReference>("sdkmessageid").Id;
            var entity = filter.GetAttributeValue<string>("primaryobjecttypecode");
            if (string.IsNullOrEmpty(entity) || entity == "none") continue;
            result[RegistrationSnapshot.FilterKey(namesById[messageId], entity)] = filter.Id;
        }

        return result;
    }

    private List<ExistingStep> GetSteps(IReadOnlyCollection<Guid> pluginTypeIds)
    {
        if (pluginTypeIds.Count == 0) return [];

        var query = new QueryExpression("sdkmessageprocessingstep")
        {
            ColumnSet = new ColumnSet("plugintypeid", "stage", "mode", "rank", "filteringattributes",
                "name", "description", "asyncautodelete"),
        };
        query.Criteria.AddCondition("plugintypeid", ConditionOperator.In, pluginTypeIds.Cast<object>().ToArray());
        var message = query.AddLink("sdkmessage", "sdkmessageid", "sdkmessageid");
        message.EntityAlias = "message";
        message.Columns = new ColumnSet("name");
        var filter = query.AddLink("sdkmessagefilter", "sdkmessagefilterid", "sdkmessagefilterid", JoinOperator.LeftOuter);
        filter.EntityAlias = "filter";
        filter.Columns = new ColumnSet("primaryobjecttypecode");

        var steps = Service.RetrieveMultiple(query).Entities;
        var images = GetImages(steps.Select(s => s.Id).ToList());

        return steps.Select(s => new ExistingStep
        {
            Id = s.Id,
            PluginTypeId = s.GetAttributeValue<EntityReference>("plugintypeid").Id,
            MessageName = (string)s.GetAttributeValue<AliasedValue>("message.name").Value,
            EntityLogicalName = s.GetAttributeValue<AliasedValue>("filter.primaryobjecttypecode")?.Value as string,
            Stage = s.GetAttributeValue<OptionSetValue>("stage").Value,
            Mode = s.GetAttributeValue<OptionSetValue>("mode")?.Value ?? 0,
            Rank = s.GetAttributeValue<int>("rank"),
            FilteringAttributes = s.GetAttributeValue<string>("filteringattributes"),
            Name = s.GetAttributeValue<string>("name") ?? string.Empty,
            Description = s.GetAttributeValue<string>("description"),
            AsyncAutoDelete = s.GetAttributeValue<bool>("asyncautodelete"),
            Images = images.TryGetValue(s.Id, out var stepImages) ? stepImages : [],
        }).ToList();
    }

    private Dictionary<Guid, List<ExistingImage>> GetImages(IReadOnlyCollection<Guid> stepIds)
    {
        if (stepIds.Count == 0) return new Dictionary<Guid, List<ExistingImage>>();

        var query = new QueryExpression("sdkmessageprocessingstepimage")
        {
            ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "imagetype", "entityalias", "attributes"),
        };
        query.Criteria.AddCondition("sdkmessageprocessingstepid", ConditionOperator.In, stepIds.Cast<object>().ToArray());

        return Service.RetrieveMultiple(query).Entities
            .GroupBy(i => i.GetAttributeValue<EntityReference>("sdkmessageprocessingstepid").Id)
            .ToDictionary(g => g.Key, g => g.Select(i => new ExistingImage(
                i.Id,
                i.GetAttributeValue<OptionSetValue>("imagetype").Value,
                i.GetAttributeValue<string>("entityalias") ?? string.Empty,
                i.GetAttributeValue<string>("attributes"))).ToList());
    }
}
