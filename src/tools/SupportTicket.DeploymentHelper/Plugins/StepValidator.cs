namespace SupportTicket.DeploymentHelper.Plugins;

/// <summary>Offline checks of declared steps against Dataverse registration rules.</summary>
public static class StepValidator
{
    private static readonly HashSet<string> MessagesWithImages = new(StringComparer.OrdinalIgnoreCase)
    {
        "Create", "Update", "Delete", "Assign", "SetState", "SetStateDynamicEntity", "Merge",
    };

    public static IReadOnlyList<string> Validate(IReadOnlyList<DeclaredStep> steps)
    {
        var errors = new List<string>();

        foreach (var step in steps)
        {
            var label = $"{step.PluginTypeName} [{step.MessageName} {step.EntityLogicalName ?? "(unbound)"} {StepStage.Name(step.Stage)}]";

            if (string.IsNullOrWhiteSpace(step.MessageName))
                errors.Add($"{label}: message name is required.");

            // Without a table a step has no message filter and runs for every table. Only Custom
            // APIs and Custom Actions (ActionStep) can be genuinely unbound.
            if (step.Kind == StepKind.PluginStep && step.EntityLogicalName is null)
                errors.Add($"{label}: PluginStep needs a table; without one the step would run for every table. Use ActionStep for unbound Custom APIs or Actions.");

            if (step.IsMainOperation)
            {
                if (step.Kind != StepKind.ActionStep)
                    errors.Add($"{label}: IsMainOperation is only valid on ActionStep.");
                if (step.Async || step.Images.Count > 0 || step.FilteringAttributes is not null)
                    errors.Add($"{label}: a main operation handler cannot be async or have images or filtering attributes.");
                continue;
            }

            if (step.Stage is not (StepStage.PreValidation or StepStage.PreOperation or StepStage.PostOperation))
                errors.Add($"{label}: stage must be PreValidation, PreOperation or PostOperation.");

            if (step.Async && step.Stage != StepStage.PostOperation)
                errors.Add($"{label}: asynchronous steps must run in PostOperation.");

            if (step.ExecutionOrder < 1)
                errors.Add($"{label}: executionOrder must be 1 or higher.");

            if (step.FilteringAttributes is not null && !string.Equals(step.MessageName, "Update", StringComparison.OrdinalIgnoreCase))
                errors.Add($"{label}: filtering attributes are only supported on Update.");

            foreach (var image in step.Images)
            {
                if (!MessagesWithImages.Contains(step.MessageName))
                    errors.Add($"{label}: entity images are not supported for {step.MessageName}.");
                else if (image.ImageType == ImageType.PreImage && string.Equals(step.MessageName, "Create", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{label}: Create has no pre-image.");
                else if (image.ImageType == ImageType.PostImage && step.Stage != StepStage.PostOperation)
                    errors.Add($"{label}: post-images are only available in PostOperation.");
                else if (image.ImageType == ImageType.PostImage && string.Equals(step.MessageName, "Delete", StringComparison.OrdinalIgnoreCase))
                    errors.Add($"{label}: Delete has no post-image.");
            }
        }

        foreach (var duplicate in steps.Where(s => !s.IsMainOperation).GroupBy(s => s.Key).Where(g => g.Count() > 1))
        {
            errors.Add($"Step declared more than once: {duplicate.Key}.");
        }

        // A Custom API main handler (IsMainOperation) and an ordinary step of the same plug-in on the
        // same message would run the plug-in twice per call.
        foreach (var main in steps.Where(s => s.IsMainOperation))
        {
            if (steps.Any(s => !s.IsMainOperation && s.PluginTypeName == main.PluginTypeName
                               && string.Equals(s.MessageName, main.MessageName, StringComparison.OrdinalIgnoreCase)))
                errors.Add($"{main.PluginTypeName} is declared both as main handler and as a step of '{main.MessageName}'.");
        }

        foreach (var duplicate in steps.Where(s => s.IsMainOperation).GroupBy(s => s.MessageName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            errors.Add($"Custom API '{duplicate.Key}' has more than one declared main handler.");
        }

        return errors;
    }

    /// <summary>Message property that carries the entity for images.</summary>
    public static string ImageMessageProperty(string messageName) => messageName switch
    {
        _ when string.Equals(messageName, "Create", StringComparison.OrdinalIgnoreCase) => "Id",
        _ when messageName.StartsWith("SetState", StringComparison.OrdinalIgnoreCase) => "EntityMoniker",
        _ => "Target",
    };
}
