
namespace SupportTicket.EntityGenerator.Configuration;

public sealed class GenerationSettings
{
    public string SolutionUniqueName { get; set; } = string.Empty;
    public string PublisherPrefix { get; set; } = string.Empty;
    public GenerationProfiles Generation { get; set; } = new();
    public OutputSettings Output { get; set; } = new();
}

public sealed class GenerationProfiles
{
    public FrontendGenerationSettings Frontend { get; set; } = new();
    public BackendGenerationSettings Backend { get; set; } = new();
}

public sealed class FrontendGenerationSettings
{
    public bool Enabled { get; set; }
    public List<string> Tables { get; set; } = new();
    public bool IncludeChoices { get; set; }
    public bool IncludeRelationships { get; set; }
}


public sealed class BackendGenerationSettings
{
    public bool Enabled { get; set; }
    public string Scope { get; set; } = "all";
    public List<string> IncludeTables { get; set; } = new();
    public bool IncludeGlobalChoices { get; set; }
    public bool IncludeOptionSets { get; set; }
    public bool IncludeRelationships { get; set; }
}


public sealed class OutputSettings
{
    public string TypeScript { get; set; } = string.Empty;
    public string CSharp { get; set; } = string.Empty;
    public string CSharpNamespace { get; set; } = string.Empty;
}
