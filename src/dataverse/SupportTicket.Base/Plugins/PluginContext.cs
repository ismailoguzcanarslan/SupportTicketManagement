using System;
using Microsoft.Xrm.Sdk;

namespace SupportTicket.Base.Plugins;

/// <summary>
/// Everything a plug-in needs for one execution, resolved once from the sandbox IServiceProvider.
/// Organization services are created lazily, so plug-ins that do not call Dataverse pay nothing.
/// </summary>
public sealed class PluginContext
{
    private const string TargetParameter = "Target";

    private readonly IOrganizationServiceFactory _serviceFactory;
    private IOrganizationService? _userService;
    private IOrganizationService? _systemService;

    public PluginContext(IServiceProvider serviceProvider)
    {
        if (serviceProvider == null) throw new ArgumentNullException(nameof(serviceProvider));

        ExecutionContext = GetRequiredService<IPluginExecutionContext>(serviceProvider);
        TracingService = GetRequiredService<ITracingService>(serviceProvider);
        _serviceFactory = GetRequiredService<IOrganizationServiceFactory>(serviceProvider);
    }

    public IPluginExecutionContext ExecutionContext { get; }

    public ITracingService TracingService { get; }

    /// <summary>Organization service running as the user the step executes for (context.UserId).</summary>
    public IOrganizationService OrganizationService =>
        _userService ??= _serviceFactory.CreateOrganizationService(ExecutionContext.UserId);

    /// <summary>Organization service running as SYSTEM. Use only when the user's privileges must not apply.</summary>
    public IOrganizationService SystemOrganizationService =>
        _systemService ??= _serviceFactory.CreateOrganizationService(null);

    public string MessageName => ExecutionContext.MessageName;

    public string PrimaryEntityName => ExecutionContext.PrimaryEntityName;

    public int Stage => ExecutionContext.Stage;

    public int Depth => ExecutionContext.Depth;

    public Guid CorrelationId => ExecutionContext.CorrelationId;

    public ParameterCollection InputParameters => ExecutionContext.InputParameters;

    public ParameterCollection OutputParameters => ExecutionContext.OutputParameters;

    /// <summary>Target as a late-bound entity (Create, Update), or null when Target is not an entity.</summary>
    public Entity? Target =>
        InputParameters.TryGetValue(TargetParameter, out var value) ? value as Entity : null;

    /// <summary>Target as an entity reference (Delete, Assign, SetState, ...), or null.</summary>
    public EntityReference? TargetReference =>
        InputParameters.TryGetValue(TargetParameter, out var value) ? value as EntityReference : null;

    /// <summary>
    /// Target converted to an early-bound type. Throws if Target is missing, so a step registered on
    /// the wrong message fails loudly instead of silently doing nothing.
    /// </summary>
    public T GetTarget<T>()
        where T : Entity
    {
        var target = Target ?? throw new InvalidPluginExecutionException(
            $"Message '{MessageName}' has no entity Target parameter.");
        return target.ToEntity<T>();
    }

    /// <summary>Pre-image by alias as an early-bound type, or null if it was not registered/provided.</summary>
    public T? GetPreImage<T>(string name)
        where T : Entity =>
        ExecutionContext.PreEntityImages.TryGetValue(name, out var image) ? image.ToEntity<T>() : null;

    /// <summary>Post-image by alias as an early-bound type, or null if it was not registered/provided.</summary>
    public T? GetPostImage<T>(string name)
        where T : Entity =>
        ExecutionContext.PostEntityImages.TryGetValue(name, out var image) ? image.ToEntity<T>() : null;

    public void Trace(string format, params object[] args) => TracingService.Trace(format, args);

    private static T GetRequiredService<T>(IServiceProvider serviceProvider)
        where T : class =>
        serviceProvider.GetService(typeof(T)) as T
        ?? throw new InvalidPluginExecutionException($"Service '{typeof(T).Name}' is not available.");
}
