using Microsoft.Xrm.Sdk;
using NSubstitute;
using SupportTicket.Base.Registration;

namespace SupportTicket.PluginTests.Infrastructure;

/// <summary>
/// Everything one plug-in execution needs, wired the way the Dataverse pipeline provides it:
/// execution context, tracing, organization service (an NSubstitute substitute) and entity images.
/// </summary>
/// <example>
/// var test = new PluginTestContext("Update", PluginStage.PreOperation, "ogz_support_ticket")
///     .WithTarget(target)
///     .WithPreImage("PreImage", preImage);
/// new SomePlugin().Execute(test.ServiceProvider);
/// </example>
public sealed class PluginTestContext
{
    public PluginTestContext(string messageName, PluginStage stage, string primaryEntityName)
    {
        ExecutionContext = new FakePluginExecutionContext
        {
            MessageName = messageName,
            Stage = (int)stage,
            PrimaryEntityName = primaryEntityName,
        };
        Tracing = new FakeTracingService();
        OrganizationService = Substitute.For<IOrganizationService>();
        ServiceFactory = new FakeOrganizationServiceFactory(OrganizationService);
        ServiceProvider = new FakeServiceProvider()
            .Add<IPluginExecutionContext>(ExecutionContext)
            .Add<ITracingService>(Tracing)
            .Add<IOrganizationServiceFactory>(ServiceFactory);
    }

    public FakePluginExecutionContext ExecutionContext { get; }

    public FakeTracingService Tracing { get; }

    /// <summary>Substitute: configure with <c>.Returns(...)</c>, verify with <c>.Received()</c>.</summary>
    public IOrganizationService OrganizationService { get; }

    public FakeOrganizationServiceFactory ServiceFactory { get; }

    public FakeServiceProvider ServiceProvider { get; }

    /// <summary>Sets InputParameters["Target"] to an entity (Create/Update) and the primary entity id.</summary>
    public PluginTestContext WithTarget(Entity target)
    {
        ExecutionContext.InputParameters["Target"] = target;
        ExecutionContext.PrimaryEntityId = target.Id;
        return this;
    }

    /// <summary>Sets InputParameters["Target"] to an entity reference (Delete, Assign, ...) and the primary entity id.</summary>
    public PluginTestContext WithTarget(EntityReference target)
    {
        ExecutionContext.InputParameters["Target"] = target;
        ExecutionContext.PrimaryEntityId = target.Id;
        return this;
    }

    public PluginTestContext WithPreImage(string name, Entity image)
    {
        ExecutionContext.PreEntityImages[name] = image;
        return this;
    }

    public PluginTestContext WithPostImage(string name, Entity image)
    {
        ExecutionContext.PostEntityImages[name] = image;
        return this;
    }
}
