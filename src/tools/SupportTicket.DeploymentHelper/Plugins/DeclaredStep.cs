namespace SupportTicket.DeploymentHelper.Plugins;

public enum StepKind
{
    PluginStep,
    ActionStep,
}

public static class StepStage
{
    public const int PreValidation = 10;
    public const int PreOperation = 20;
    public const int PostOperation = 40;

    public static string Name(int stage) => stage switch
    {
        PreValidation => "PreValidation",
        PreOperation => "PreOperation",
        PostOperation => "PostOperation",
        _ => stage.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };
}

public static class ImageType
{
    public const int PreImage = 0;
    public const int PostImage = 1;
}

public sealed record DeclaredImage(int ImageType, string Alias, string? Attributes);

/// <summary>A step declared by a PluginStep/ActionStep attribute, normalized for comparison.</summary>
public sealed record DeclaredStep
{
    public required string PluginTypeName { get; init; }
    public required StepKind Kind { get; init; }
    public required string MessageName { get; init; }

    /// <summary>Null for unbound messages.</summary>
    public string? EntityLogicalName { get; init; }

    public required int Stage { get; init; }
    public bool Async { get; init; }
    public int ExecutionOrder { get; init; } = 1;

    /// <summary>Sorted, lower-case, comma-separated; null = all attributes.</summary>
    public string? FilteringAttributes { get; init; }

    public required string Name { get; init; }
    public string? Description { get; init; }
    public bool DeleteAsyncOperationIfSuccessful { get; init; } = true;
    public bool IsMainOperation { get; init; }
    public IReadOnlyList<DeclaredImage> Images { get; init; } = [];

    /// <summary>Identity used to match an existing registration: plug-in type + message + table + stage.</summary>
    public string Key => StepKey.For(PluginTypeName, MessageName, EntityLogicalName, Stage);

    public static string DefaultName(string pluginTypeName, string messageName, string? entity, int stage) =>
        $"{pluginTypeName}: {messageName} of {entity ?? "any entity"} ({StepStage.Name(stage)})";
}

public static class StepKey
{
    public static string For(string pluginTypeName, string messageName, string? entity, int stage) =>
        string.Join("|", pluginTypeName, messageName, entity ?? "none", stage).ToLowerInvariant();
}

public static class AttributeList
{
    /// <summary>Normalizes "b, A ,a" to "a,b"; empty becomes null.</summary>
    public static string? Normalize(string? attributes)
    {
        if (string.IsNullOrWhiteSpace(attributes)) return null;

        var parts = attributes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(a => a.ToLowerInvariant())
            .Distinct()
            .Order(StringComparer.Ordinal)
            .ToArray();
        return parts.Length == 0 ? null : string.Join(",", parts);
    }

    /// <summary>Treats null, "", "none" as an unbound message.</summary>
    public static string? NormalizeEntity(string? entity) =>
        string.IsNullOrWhiteSpace(entity) || string.Equals(entity, "none", StringComparison.OrdinalIgnoreCase)
            ? null
            : entity.Trim().ToLowerInvariant();
}
