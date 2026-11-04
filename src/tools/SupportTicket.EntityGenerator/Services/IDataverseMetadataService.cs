using Microsoft.Xrm.Sdk.Metadata;

namespace SupportTicket.EntityGenerator.Services;

public interface IDataverseMetadataService
{
    Task<EntityMetadata> GetEntityMetadataAsync(string entityLogicalName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EntityMetadata>> GetAllEntitiesMetadataAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSolutionEntityLogicalNamesAsync(string solutionUniqueName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OptionSetMetadata>> GetUsedGlobalChoicesAsync(IReadOnlyList<string> tableLogicalNames, CancellationToken cancellationToken = default);
}
