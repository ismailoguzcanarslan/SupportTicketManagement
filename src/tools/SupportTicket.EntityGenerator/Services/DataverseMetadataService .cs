using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace SupportTicket.EntityGenerator.Services;

public class DataverseMetadataService : IDataverseMetadataService
{
    private readonly ServiceClient _serviceClient;

    public DataverseMetadataService(ServiceClient serviceClient)
    {
        _serviceClient = serviceClient ?? throw new ArgumentNullException(nameof(serviceClient));
    }

    public async Task<EntityMetadata> GetEntityMetadataAsync(string entityLogicalName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(entityLogicalName);
        cancellationToken.ThrowIfCancellationRequested();

        var request = new RetrieveEntityRequest
        {
            LogicalName = entityLogicalName,
            EntityFilters = EntityFilters.All,
            RetrieveAsIfPublished = false
        };

        var response = (RetrieveEntityResponse)await _serviceClient.ExecuteAsync(request,cancellationToken);

        return response.EntityMetadata;
    }

    public async Task<IReadOnlyList<EntityMetadata>> GetAllEntitiesMetadataAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        };

        var response = (RetrieveAllEntitiesResponse)await _serviceClient.ExecuteAsync(request, cancellationToken);

        return response.EntityMetadata;
    }



    public async Task<IReadOnlyList<string>> GetSolutionEntityLogicalNamesAsync(string solutionUniqueName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(solutionUniqueName))
            throw new ArgumentException("Solution unique name boş olamaz.", nameof(solutionUniqueName));

        var solutionQuery = new QueryExpression("solution")
        {
            ColumnSet = new ColumnSet("solutionid"),
            TopCount = 1
        };

        solutionQuery.Criteria.AddCondition("uniquename", ConditionOperator.Equal, solutionUniqueName);

        var solutions = await _serviceClient.RetrieveMultipleAsync(solutionQuery, cancellationToken);

        if (solutions.Entities.Count == 0)
            throw new InvalidOperationException($"Solution bulunamadı: {solutionUniqueName}");

        var solutionId = solutions.Entities[0].Id;

        var componentQuery = new QueryExpression("solutioncomponent")
        {
            ColumnSet = new ColumnSet("objectid")
        };

        componentQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
        componentQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, 1); // 1 = Entity (Table)

        var components = await _serviceClient.RetrieveMultipleAsync(componentQuery, cancellationToken);

        var entityIds = components.Entities.Select(x => x.GetAttributeValue<Guid>("objectid")).ToHashSet();

        var request = new RetrieveAllEntitiesRequest
        {
            EntityFilters = EntityFilters.Entity,
            RetrieveAsIfPublished = false
        };

        var response = (RetrieveAllEntitiesResponse)await _serviceClient.ExecuteAsync(request, cancellationToken);

        return response.EntityMetadata
            .Where(x => x.MetadataId.HasValue && entityIds.Contains(x.MetadataId.Value))
            .Select(x => x.LogicalName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public async Task<IReadOnlyList<OptionSetMetadata>> GetUsedGlobalChoicesAsync(IReadOnlyList<string> tableLogicalNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableLogicalNames);

        var globalChoices = new Dictionary<string, OptionSetMetadata>(StringComparer.OrdinalIgnoreCase);

        foreach (var tableName in tableLogicalNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = await GetEntityMetadataAsync(tableName, cancellationToken);

            foreach (var attribute in metadata.Attributes ?? [])
            {
                if (attribute is not EnumAttributeMetadata choiceAttribute)
                    continue;

                var optionSet = choiceAttribute.OptionSet;

                if (optionSet?.IsGlobal != true || string.IsNullOrWhiteSpace(optionSet.Name))
                    continue;

                if (optionSet is OptionSetMetadata globalChoice)
                    globalChoices.TryAdd(globalChoice.Name, globalChoice);
            }
        }

        return globalChoices.Values.OrderBy(x => x.Name).ToList();
    }
}
