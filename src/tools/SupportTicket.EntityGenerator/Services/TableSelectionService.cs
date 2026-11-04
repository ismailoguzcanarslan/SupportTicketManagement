
using SupportTicket.EntityGenerator.Configuration;

namespace SupportTicket.EntityGenerator.Services;

public sealed class TableSelectionService
{
    private readonly IDataverseMetadataService _metadataService;

    public TableSelectionService(IDataverseMetadataService metadataService)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
    }

    public async Task<IReadOnlyList<string>> GetBackendTablesAsync(GenerationSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!string.Equals(settings.Generation.Backend.Scope, "solution", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Desteklenmeyen backend scope: {settings.Generation.Backend.Scope}");

        // Solution içerisindeki tabloları Dataverse'den getiriyoruz.
        var solutionTables = await _metadataService.GetSolutionEntityLogicalNamesAsync(settings.SolutionUniqueName, cancellationToken);

        // Solution tablolarına JSON'daki ek tabloları dahil ediyoruz.
        return solutionTables
            .Concat(settings.Generation.Backend.IncludeTables)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }
}
