using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using SupportTicket.Base.Plugins;
using SupportTicket.Base.Registration;
using SupportTicket.PluginTests.Infrastructure;
using Xunit;

namespace SupportTicket.PluginTests.Base;

// Context, services and failure behavior of BasePlugin/PluginContext that the core tests do not cover.
public class BasePluginContextTests
{
    private const string TicketEntity = "ogz_support_ticket";

    [Fact]
    public void Context_ExposesMessageStageDepthAndParameterCollections()
    {
        var test = new PluginTestContext(MessageName.Update, PluginStage.PostOperation, TicketEntity);
        test.ExecutionContext.Depth = 2;

        var context = new PluginContext(test.ServiceProvider);

        Assert.Equal(MessageName.Update, context.MessageName);
        Assert.Equal(TicketEntity, context.PrimaryEntityName);
        Assert.Equal((int)PluginStage.PostOperation, context.Stage);
        Assert.Equal(2, context.Depth);
        Assert.Equal(test.ExecutionContext.CorrelationId, context.CorrelationId);
        Assert.Same(test.ExecutionContext.InputParameters, context.InputParameters);
        Assert.Same(test.ExecutionContext.OutputParameters, context.OutputParameters);
    }

    [Fact]
    public void OrganizationService_RunsAsUserId_NotInitiatingUserId()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);
        test.ExecutionContext.UserId = Guid.NewGuid();
        test.ExecutionContext.InitiatingUserId = Guid.NewGuid();

        _ = new PluginContext(test.ServiceProvider).OrganizationService;

        Assert.Equal(new Guid?[] { test.ExecutionContext.UserId }, test.ServiceFactory.RequestedUserIds);
    }

    [Fact]
    public void Target_IsNullWhenTheMessageHasNoTarget()
    {
        var context = new PluginContext(new PluginTestContext("ogz_CustomMessage", PluginStage.PostOperation, TicketEntity).ServiceProvider);

        Assert.Null(context.Target);
        Assert.Null(context.TargetReference);
        Assert.Throws<InvalidPluginExecutionException>(() => context.GetTarget<Entity>());
    }

    [Fact]
    public void Target_OfAnotherTypeIsNotReturnedAsEntity()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);
        test.ExecutionContext.InputParameters["Target"] = "not an entity";
        var context = new PluginContext(test.ServiceProvider);

        Assert.Null(context.Target);
        Assert.Null(context.TargetReference);
    }

    [Fact]
    public void Trace_WritesToTheTracingService()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);

        new PluginContext(test.ServiceProvider).Trace("value {0}", 42);

        Assert.Contains("value 42", test.Tracing.Messages);
    }

    [Theory]
    [InlineData(typeof(ITracingService))]
    [InlineData(typeof(IOrganizationServiceFactory))]
    [InlineData(typeof(IPluginExecutionContext))]
    public void MissingPipelineService_StopsBeforeThePluginBodyRuns(Type missing)
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);
        var provider = new FakeServiceProvider();
        if (missing != typeof(IPluginExecutionContext)) provider.Add<IPluginExecutionContext>(test.ExecutionContext);
        if (missing != typeof(ITracingService)) provider.Add<ITracingService>(test.Tracing);
        if (missing != typeof(IOrganizationServiceFactory)) provider.Add<IOrganizationServiceFactory>(test.ServiceFactory);
        var bodyRan = false;

        var error = Assert.Throws<InvalidPluginExecutionException>(() => new ProbePlugin(_ => bodyRan = true).Execute(provider));

        Assert.False(bodyRan);
        Assert.Contains(missing.Name, error.Message);
    }

    [Fact]
    public void Execute_WithNullServiceProvider_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ProbePlugin(_ => { }).Execute(null!));
    }

    [Fact]
    public void UnexpectedError_MessageNamesThePlugin_AndTraceKeepsTheDetails()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);

        var error = Assert.Throws<InvalidPluginExecutionException>(
            () => new ProbePlugin(_ => throw new NullReferenceException("detail")).Execute(test.ServiceProvider));

        Assert.StartsWith("An unexpected error occurred in ProbePlugin", error.Message);
        Assert.DoesNotContain("detail", error.Message);
        Assert.Contains(test.Tracing.Messages, m => m.Contains("NullReferenceException") && m.Contains("detail"));
    }

    [Fact]
    public void DataverseFault_IsTracedWithItsErrorCode()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);
        var fault = new FaultException<OrganizationServiceFault>(new OrganizationServiceFault { ErrorCode = -2147220969 }, "Not found");

        Assert.Throws<InvalidPluginExecutionException>(() => new ProbePlugin(_ => throw fault).Execute(test.ServiceProvider));

        Assert.Contains(test.Tracing.Messages, m => m.Contains("Dataverse fault -2147220969") && m.Contains("Not found"));
    }

    [Fact]
    public void BusinessErrorMessage_ReachesTheUserUnchanged_AndIsNotTracedAsUnexpected()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreValidation, TicketEntity);

        var error = Assert.Throws<InvalidPluginExecutionException>(
            () => new ProbePlugin(_ => throw new InvalidPluginExecutionException("Title is required.")).Execute(test.ServiceProvider));

        Assert.Equal("Title is required.", error.Message);
        Assert.DoesNotContain(test.Tracing.Messages, m => m.Contains("unexpected error"));
    }

    [Fact]
    public void StepConfiguration_IsAvailableToDerivedPlugins()
    {
        var plugin = new ConfiguredPlugin("unsecure", "secure");

        Assert.Equal("unsecure", plugin.Unsecure);
        Assert.Equal("secure", plugin.Secure);
    }

    private sealed class ProbePlugin : BasePlugin
    {
        private readonly Action<PluginContext> _body;

        public ProbePlugin(Action<PluginContext> body)
        {
            _body = body;
        }

        protected override void ExecutePlugin(PluginContext context) => _body(context);
    }

    private sealed class ConfiguredPlugin : BasePlugin
    {
        public ConfiguredPlugin(string unsecure, string secure)
            : base(unsecure, secure)
        {
        }

        public string? Unsecure => UnsecureConfiguration;

        public string? Secure => SecureConfiguration;

        protected override void ExecutePlugin(PluginContext context)
        {
        }
    }
}
