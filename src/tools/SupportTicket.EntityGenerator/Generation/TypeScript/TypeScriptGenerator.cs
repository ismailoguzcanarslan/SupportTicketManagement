
using Microsoft.Xrm.Sdk.Metadata;
using SupportTicket.EntityGenerator.Services;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SupportTicket.EntityGenerator.Generation.TypeScript;

public class TypeScriptGenerator
{
    private readonly IDataverseMetadataService _metadataService;

    public TypeScriptGenerator(IDataverseMetadataService metadataService)
    {
        _metadataService = metadataService ?? throw new ArgumentNullException(nameof(metadataService));
    }

    public async Task GenerateAsync(IReadOnlyList<string> tables, string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        var entitiesDirectory = Path.Combine(outputDirectory, "entities");
        var choicesDirectory = Path.Combine(outputDirectory, "choices");

        Directory.CreateDirectory(entitiesDirectory);
        Directory.CreateDirectory(choicesDirectory);

        var globalChoices = new Dictionary<string, OptionSetMetadataBase>(
            StringComparer.OrdinalIgnoreCase);

        var entityNames = new List<string>();

        foreach (var table in tables.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = await _metadataService.GetEntityMetadataAsync(table, cancellationToken);

            if (string.IsNullOrWhiteSpace(metadata.LogicalName))
                continue;

            var content = GenerateEntity(metadata, globalChoices);
            var filePath = Path.Combine(entitiesDirectory, $"{metadata.LogicalName}.ts");

            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);
            entityNames.Add(metadata.LogicalName);
        }

        foreach (var choice in globalChoices.OrderBy(x => x.Key))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var choiceName = GetGlobalChoiceName(choice.Key);
            var options = GetChoiceOptions(choice.Value);

            var content = new StringBuilder();
            AppendHeader(content);
            content.AppendLine(GenerateChoiceEnum(choiceName, options));
            content.AppendLine();
            content.AppendLine(GenerateChoiceOptions(choiceName, options));

            var filePath = Path.Combine(choicesDirectory, $"{choice.Key}.ts");

            await File.WriteAllTextAsync(filePath, content.ToString(), Encoding.UTF8, cancellationToken);
        }

        await GenerateIndexAsync(outputDirectory, entityNames, globalChoices.Keys, cancellationToken);
    }

    private static string GenerateEntity(EntityMetadata metadata, Dictionary<string, OptionSetMetadataBase> globalChoices)
    {
        var builder = new StringBuilder();
        var entityName = metadata.LogicalName!;

        var attributes = (metadata.Attributes ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x.LogicalName))
            .Select(x => new
            {
                Metadata = x,
                Name = x.LogicalName!,
                Type = GetTypeScriptType(x)
            })
            .Where(x => x.Type is not null)
            .OrderBy(x => x.Name)
            .ToList();

        var localChoices = new Dictionary<string, IReadOnlyList<OptionMetadata>>(
            StringComparer.OrdinalIgnoreCase);

        var imports = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        var fields = new List<(string Name, string Type)>();

        foreach (var attribute in attributes)
        {
            var type = attribute.Type!;

            if (attribute.Metadata is EnumAttributeMetadata choiceAttribute &&
                choiceAttribute.OptionSet is { } optionSet)
            {
                var isMultiSelect = attribute.Metadata is MultiSelectPicklistAttributeMetadata;

                if (optionSet.IsGlobal == true && !string.IsNullOrWhiteSpace(optionSet.Name))
                {
                    var globalName = optionSet.Name;
                    var enumName = GetGlobalChoiceName(globalName);

                    globalChoices.TryAdd(globalName, optionSet);
                    imports.TryAdd(globalName, enumName);

                    type = isMultiSelect ? $"{enumName}[]" : enumName;
                }
                else
                {
                    var enumName = GetLocalChoiceName(entityName, attribute.Name);
                    localChoices.TryAdd(enumName, GetChoiceOptions(optionSet));

                    type = isMultiSelect ? $"{enumName}[]" : enumName;
                }
            }

            fields.Add((attribute.Name, type));
        }

        AppendHeader(builder);

        foreach (var import in imports.OrderBy(x => x.Key))
            builder.AppendLine($"import {{ {import.Value} }} from \"../choices/{import.Key}\";");

        if (imports.Count > 0)
            builder.AppendLine();

        builder.AppendLine($"export interface {entityName} {{");

        foreach (var field in fields)
            builder.AppendLine($"    {field.Name}?: {field.Type} | null;");

        builder.AppendLine("}");
        builder.AppendLine();

        builder.AppendLine($"export const {entityName}Fields = {{");

        foreach (var field in fields)
            builder.AppendLine($"    {field.Name}: \"{field.Name}\",");

        builder.AppendLine("} as const;");

        foreach (var choice in localChoices.OrderBy(x => x.Key))
        {
            builder.AppendLine();
            builder.AppendLine(GenerateChoiceEnum(choice.Key, choice.Value));
            builder.AppendLine();
            builder.AppendLine(GenerateChoiceOptions(choice.Key, choice.Value));
        }

        return builder.ToString();
    }

    private static string? GetTypeScriptType(AttributeMetadata attribute)
    {
        if (attribute is MultiSelectPicklistAttributeMetadata)
            return "number[]";

        return attribute.AttributeType switch
        {
            AttributeTypeCode.String or
            AttributeTypeCode.Memo or
            AttributeTypeCode.Uniqueidentifier or
            AttributeTypeCode.EntityName => "string",

            AttributeTypeCode.Integer or
            AttributeTypeCode.BigInt or
            AttributeTypeCode.Decimal or
            AttributeTypeCode.Double or
            AttributeTypeCode.Money or
            AttributeTypeCode.Picklist or
            AttributeTypeCode.State or
            AttributeTypeCode.Status => "number",

            AttributeTypeCode.Boolean => "boolean",

            AttributeTypeCode.DateTime => "string",

            AttributeTypeCode.Lookup or
            AttributeTypeCode.Customer or
            AttributeTypeCode.Owner => "string",

            _ => null
        };
    }

    private static IReadOnlyList<OptionMetadata> GetChoiceOptions(AttributeMetadata attribute)
    {
        if (attribute is not EnumAttributeMetadata choiceAttribute)
            return [];

        return choiceAttribute.OptionSet is { } optionSet
            ? GetChoiceOptions(optionSet)
            : [];
    }

    private static IReadOnlyList<OptionMetadata> GetChoiceOptions(OptionSetMetadataBase optionSet)
    {
        return optionSet is OptionSetMetadata choices
            ? (choices.Options ?? [])
                .Where(x => x.Value.HasValue)
                .OrderBy(x => x.Value)
                .ToList()
            : [];
    }

    private static string GenerateChoiceOptions(string choiceName, IReadOnlyList<OptionMetadata> options)
    {
        var builder = new StringBuilder();

        builder.AppendLine($"export const {choiceName}Options = [");

        foreach (var option in options)
        {
            var value = option.Value!.Value;
            var text = GetOptionLabel(option);

            builder.AppendLine(
                $"    {{ value: {value}, text: {JsonSerializer.Serialize(text)} }},");
        }

        builder.AppendLine("] as const;");

        return builder.ToString();
    }

    private static string GenerateChoiceEnum(string choiceName, IReadOnlyList<OptionMetadata> options)
    {
        var builder = new StringBuilder();
        var usedNames = new HashSet<string>(StringComparer.Ordinal);

        builder.AppendLine($"export enum {choiceName} {{");

        foreach (var option in options)
        {
            var value = option.Value!.Value;
            var label = GetOptionLabel(option);
            var baseName = ToIdentifier(label, $"Value_{value}");
            var memberName = baseName;

            if (!usedNames.Add(memberName))
            {
                memberName = $"{baseName}_{value}";
                var suffix = 2;

                while (!usedNames.Add(memberName))
                    memberName = $"{baseName}_{value}_{suffix++}";
            }

            builder.AppendLine($"    {memberName} = {value},");
        }

        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string GetOptionLabel(OptionMetadata option)
    {
        return option.Label?.UserLocalizedLabel?.Label
            ?? option.Label?.LocalizedLabels?.FirstOrDefault()?.Label
            ?? $"Value_{option.Value}";
    }

    private static string GetLocalChoiceName(string entityName, string attributeName)
    {
        return ToIdentifier($"{entityName}_{attributeName}", "LocalChoice");
    }

    private static string GetGlobalChoiceName(string optionSetName)
    {
        return ToIdentifier(optionSetName, "GlobalChoice");
    }

    private static string ToIdentifier(string value, string fallback)
    {
        var result = Regex.Replace(value, @"[^A-Za-z0-9_$]", "_");

        if (string.IsNullOrWhiteSpace(result))
            return fallback;

        if (char.IsDigit(result[0]))
            result = "_" + result;

        if (TypeScriptReservedWords.Contains(result))
            result = "_" + result;

        return result;
    }

    private static readonly HashSet<string> TypeScriptReservedWords = new(StringComparer.Ordinal)
    {
        "break", "case", "catch", "class", "const", "continue",
        "debugger", "default", "delete", "do", "else", "enum",
        "export", "extends", "false", "finally", "for", "function",
        "if", "import", "in", "instanceof", "new", "null",
        "return", "super", "switch", "this", "throw", "true",
        "try", "typeof", "var", "void", "while", "with",
        "yield", "await", "implements", "interface", "let",
        "package", "private", "protected", "public", "static"
    };

    private static void AppendHeader(StringBuilder builder)
    {
        builder.AppendLine("// <auto-generated>");
        builder.AppendLine("// Generated from Dataverse metadata. Do not edit manually.");
        builder.AppendLine("// </auto-generated>");
        builder.AppendLine();
    }

    private static async Task GenerateIndexAsync(string outputDirectory, IReadOnlyList<string> entityNames, IEnumerable<string> globalChoiceNames, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        AppendHeader(builder);

        foreach (var entityName in entityNames.OrderBy(x => x))
            builder.AppendLine($"export * from \"./entities/{entityName}\";");

        foreach (var choiceName in globalChoiceNames.OrderBy(x => x))
            builder.AppendLine($"export * from \"./choices/{choiceName}\";");

        if (entityNames.Count == 0 && !globalChoiceNames.Any())
            builder.AppendLine("export {};");

        var indexPath = Path.Combine(outputDirectory, "index.ts");
        await File.WriteAllTextAsync(indexPath, builder.ToString(), Encoding.UTF8, cancellationToken);
    }
}
