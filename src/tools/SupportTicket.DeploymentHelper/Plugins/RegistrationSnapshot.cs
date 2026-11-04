namespace SupportTicket.DeploymentHelper.Plugins;

/// <summary>Read-only view of the current registrations in Dataverse, as needed for planning.</summary>
public sealed record RegistrationSnapshot
{
    public ExistingPackage? Package { get; init; }

    /// <summary>Plug-in types of the package, by full type name.</summary>
    public IReadOnlyDictionary<string, Guid> PluginTypes { get; init; } = new Dictionary<string, Guid>(StringComparer.Ordinal);

    /// <summary>SDK messages by name (case-insensitive).</summary>
    public IReadOnlyDictionary<string, SdkMessageInfo> Messages { get; init; } = new Dictionary<string, SdkMessageInfo>(StringComparer.OrdinalIgnoreCase);

    /// <summary>Message filters by "message|entity" (lower case).</summary>
    public IReadOnlyDictionary<string, Guid> Filters { get; init; } = new Dictionary<string, Guid>(StringComparer.Ordinal);

    /// <summary>Existing steps of the package's plug-in types.</summary>
    public IReadOnlyList<ExistingStep> Steps { get; init; } = [];

    /// <summary>
    /// Steps of plug-in types with the same class name registered outside the package (e.g. an
    /// earlier classic assembly registration). They would run in addition to the package's steps.
    /// </summary>
    public IReadOnlyList<ForeignStep> ForeignSteps { get; init; } = [];

    public static string FilterKey(string message, string entity) => $"{message}|{entity}".ToLowerInvariant();
}

public sealed record ExistingPackage(Guid Id, string Version, byte[]? Content);

public sealed record SdkMessageInfo(Guid Id, string Name, CustomApiInfo? CustomApi);

/// <param name="AllowedCustomProcessingStepType">0 = none, 1 = async only, 2 = sync and async.</param>
public sealed record CustomApiInfo(Guid Id, string UniqueName, Guid? PluginTypeId, int AllowedCustomProcessingStepType, string? BoundEntityLogicalName);

public sealed record ExistingImage(Guid Id, int ImageType, string Alias, string? Attributes);

public sealed record ForeignStep(string PluginTypeName, string AssemblyName, string StepName);

public sealed record ExistingStep
{
    public required Guid Id { get; init; }
    public required Guid PluginTypeId { get; init; }
    public required string MessageName { get; init; }
    public string? EntityLogicalName { get; init; }
    public required int Stage { get; init; }
    public int Mode { get; init; }
    public int Rank { get; init; } = 1;
    public string? FilteringAttributes { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool AsyncAutoDelete { get; init; }
    public IReadOnlyList<ExistingImage> Images { get; init; } = [];
}
