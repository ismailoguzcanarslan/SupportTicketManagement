using System.Reflection;
using System.Runtime.InteropServices;

namespace SupportTicket.DeploymentHelper.Plugins;

/// <param name="PluginTypeNames">Every concrete IPlugin class in the assembly, with or without attributes.</param>
public sealed record DiscoveryResult(IReadOnlyList<DeclaredStep> Steps, IReadOnlyList<string> Errors, IReadOnlyCollection<string> PluginTypeNames);

/// <summary>
/// Reads PluginStep/ActionStep attributes from the built net462 plug-in assembly without loading it
/// for execution (MetadataLoadContext). Attributes are matched by full name, so this tool does not
/// reference SupportTicket.Base and its Dataverse SDK version.
/// </summary>
public sealed class PluginStepDiscovery
{
    private const string PluginStepAttribute = "SupportTicket.Base.Registration.PluginStepAttribute";
    private const string ActionStepAttribute = "SupportTicket.Base.Registration.ActionStepAttribute";
    private const string PluginInterface = "Microsoft.Xrm.Sdk.IPlugin";

    public DiscoveryResult Discover(string assemblyPath)
    {
        var steps = new List<DeclaredStep>();
        var errors = new List<string>();
        var pluginTypes = new HashSet<string>(StringComparer.Ordinal);

        using var context = CreateLoadContext(assemblyPath);
        var assembly = context.LoadFromAssemblyPath(assemblyPath);

        foreach (var type in GetLoadableTypes(assembly, errors))
        {
            var isPlugin = type.IsClass && !type.IsAbstract && type.GetInterfaces().Any(i => i.FullName == PluginInterface);
            if (isPlugin) pluginTypes.Add(type.FullName!);

            var attributes = type.GetCustomAttributesData()
                .Where(a => a.AttributeType.FullName is PluginStepAttribute or ActionStepAttribute)
                .ToList();
            if (attributes.Count == 0) continue;

            if (!isPlugin)
            {
                errors.Add($"{type.FullName}: has registration attributes but is not a concrete IPlugin class.");
                continue;
            }

            foreach (var attribute in attributes)
            {
                try
                {
                    steps.Add(ToDeclaredStep(type.FullName!, attribute));
                }
                catch (FormatException ex)
                {
                    errors.Add($"{type.FullName}: {ex.Message}");
                }
            }
        }

        return new DiscoveryResult(steps, errors, pluginTypes);
    }

    internal static DeclaredStep ToDeclaredStep(string pluginTypeName, CustomAttributeData attribute)
    {
        var args = attribute.ConstructorArguments;
        var named = attribute.NamedArguments.ToDictionary(a => a.MemberName, a => a.TypedValue.Value, StringComparer.Ordinal);

        string? Named(string name) => named.TryGetValue(name, out var value) ? value as string : null;
        bool NamedBool(string name, bool fallback) => named.TryGetValue(name, out var value) && value is bool b ? b : fallback;

        var message = (string)args[0].Value!;
        var entity = AttributeList.NormalizeEntity(args[1].Value as string);
        var stage = Convert.ToInt32(args[2].Value, System.Globalization.CultureInfo.InvariantCulture);

        var images = new List<DeclaredImage>();
        if (Named("PreImageName") is { Length: > 0 } preImage)
            images.Add(new DeclaredImage(ImageType.PreImage, preImage, AttributeList.Normalize(Named("PreImageAttributes"))));
        if (Named("PostImageName") is { Length: > 0 } postImage)
            images.Add(new DeclaredImage(ImageType.PostImage, postImage, AttributeList.Normalize(Named("PostImageAttributes"))));

        return new DeclaredStep
        {
            PluginTypeName = pluginTypeName,
            Kind = attribute.AttributeType.FullName == ActionStepAttribute ? StepKind.ActionStep : StepKind.PluginStep,
            MessageName = message,
            EntityLogicalName = entity,
            Stage = stage,
            ExecutionOrder = Convert.ToInt32(args[3].Value, System.Globalization.CultureInfo.InvariantCulture),
            Async = (bool)args[4].Value!,
            FilteringAttributes = AttributeList.Normalize(Named("FilteringAttributes")),
            Name = Named("Name") is { Length: > 0 } name ? name : DeclaredStep.DefaultName(pluginTypeName, message, entity, stage),
            Description = Named("Description"),
            DeleteAsyncOperationIfSuccessful = NamedBool("DeleteAsyncOperationIfSuccessful", true),
            IsMainOperation = NamedBool("IsMainOperation", false),
            Images = images,
        };
    }

    private static MetadataLoadContext CreateLoadContext(string assemblyPath)
    {
        // The plug-in folder provides its references (Base, Entities, Microsoft.Xrm.Sdk); the running
        // runtime provides the core library that net462's mscorlib forwards to.
        var paths = Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(assemblyPath))!, "*.dll")
            .Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll"))
            .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First());

        return new MetadataLoadContext(new PathAssemblyResolver(paths), typeof(object).Assembly.GetName().Name);
    }

    private static IEnumerable<Type> GetLoadableTypes(Assembly assembly, List<string> errors)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            errors.AddRange(ex.LoaderExceptions.Where(e => e is not null).Select(e => $"Type load error: {e!.Message}"));
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
