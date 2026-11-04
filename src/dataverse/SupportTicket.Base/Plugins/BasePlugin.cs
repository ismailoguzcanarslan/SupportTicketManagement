using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;

namespace SupportTicket.Base.Plugins;

/// <summary>
/// Base class for every plug-in. Builds a <see cref="PluginContext"/>, traces entry/exit and turns
/// unexpected errors into an <see cref="InvalidPluginExecutionException"/> that carries the
/// correlation id. Exceptions are never swallowed: any exception rolls back the transaction of a
/// synchronous step, as Dataverse expects.
/// </summary>
/// <remarks>
/// Plug-in instances are cached and shared between threads by Dataverse. Derived classes must not
/// keep per-execution state in fields.
/// </remarks>
public abstract class BasePlugin : IPlugin
{
    protected BasePlugin()
    {
    }

    /// <summary>Called by Dataverse when the step has unsecure/secure configuration.</summary>
    protected BasePlugin(string? unsecureConfiguration, string? secureConfiguration)
    {
        UnsecureConfiguration = unsecureConfiguration;
        SecureConfiguration = secureConfiguration;
    }

    protected string? UnsecureConfiguration { get; }

    protected string? SecureConfiguration { get; }

    public void Execute(IServiceProvider serviceProvider)
    {
        var context = new PluginContext(serviceProvider);
        var pluginName = GetType().Name;

        context.Trace(
            "{0}: {1} {2}, stage {3}, depth {4}, correlation {5}",
            pluginName,
            context.MessageName,
            context.PrimaryEntityName,
            context.Stage,
            context.Depth,
            context.CorrelationId);

        try
        {
            ExecutePlugin(context);
        }
        catch (InvalidPluginExecutionException)
        {
            // Intended, user-facing error: keep its message and status.
            throw;
        }
        catch (FaultException<OrganizationServiceFault> ex)
        {
            context.Trace("{0}: Dataverse fault {1}: {2}", pluginName, ex.Detail?.ErrorCode ?? 0, ex.Message);
            throw new InvalidPluginExecutionException(
                $"{pluginName} failed: {ex.Message} (correlation {context.CorrelationId})", ex);
        }
        catch (Exception ex)
        {
            context.Trace("{0}: unexpected error: {1}", pluginName, ex);
            throw new InvalidPluginExecutionException(
                $"An unexpected error occurred in {pluginName} (correlation {context.CorrelationId}).", ex);
        }
    }

    protected abstract void ExecutePlugin(PluginContext context);
}
