using System;
using System.ServiceModel;
using Microsoft.Xrm.Sdk;
using SupportTicket.Base.Plugins;
using SupportTicket.Base.Registration;
using SupportTicket.PluginTests.Infrastructure;
using Xunit;

namespace SupportTicket.PluginTests.Base;

public class BasePluginTests
{
    private const string TicketEntity = "ogz_support_ticket";

    [Fact]
    public void Execute_PassesContextBuiltFromServiceProvider()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);
        PluginContext? received = null;

        new DelegatePlugin(context => received = context).Execute(test.ServiceProvider);

        Assert.NotNull(received);
        Assert.Same(test.ExecutionContext, received!.ExecutionContext);
        Assert.Same(test.Tracing, received.TracingService);
        Assert.Equal(MessageName.Create, received.MessageName);
    }

    [Fact]
    public void Execute_TracesEntryWithCorrelationId()
    {
        var test = new PluginTestContext(MessageName.Update, PluginStage.PostOperation, TicketEntity);

        new DelegatePlugin(_ => { }).Execute(test.ServiceProvider);

        var entry = Assert.Single(test.Tracing.Messages);
        Assert.Contains("DelegatePlugin: Update ogz_support_ticket, stage 40", entry);
        Assert.Contains(test.ExecutionContext.CorrelationId.ToString(), entry);
    }

    [Fact]
    public void Execute_RethrowsInvalidPluginExecutionExceptionUnchanged()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreValidation, TicketEntity);
        var original = new InvalidPluginExecutionException("Title is required.");

        var thrown = Assert.Throws<InvalidPluginExecutionException>(
            () => new DelegatePlugin(_ => throw original).Execute(test.ServiceProvider));

        Assert.Same(original, thrown);
    }

    [Fact]
    public void Execute_WrapsUnexpectedExceptionWithCorrelationIdAndTracesIt()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);
        var original = new InvalidOperationException("boom");

        var thrown = Assert.Throws<InvalidPluginExecutionException>(
            () => new DelegatePlugin(_ => throw original).Execute(test.ServiceProvider));

        Assert.Same(original, thrown.InnerException);
        Assert.Contains(test.ExecutionContext.CorrelationId.ToString(), thrown.Message);
        Assert.Contains(test.Tracing.Messages, m => m.Contains("unexpected error") && m.Contains("boom"));
    }

    [Fact]
    public void Execute_WrapsDataverseFaultAndKeepsItsMessage()
    {
        var test = new PluginTestContext(MessageName.Create, PluginStage.PreOperation, TicketEntity);
        var fault = new FaultException<OrganizationServiceFault>(
            new OrganizationServiceFault { ErrorCode = -2147220891, Message = "Record is locked." },
            "Record is locked.");

        var thrown = Assert.Throws<InvalidPluginExecutionException>(
            () => new DelegatePlugin(_ => throw fault).Execute(test.ServiceProvider));

        Assert.Same(fault, thrown.InnerException);
        Assert.Contains("Record is locked.", thrown.Message);
    }

    [Fact]
    public void Execute_FailsClearlyWhenPipelineServiceIsMissing()
    {
        var provider = new FakeServiceProvider().Add<ITracingService>(new FakeTracingService());

        var thrown = Assert.Throws<InvalidPluginExecutionException>(
            () => new DelegatePlugin(_ => { }).Execute(provider));

        Assert.Contains("IPluginExecutionContext", thrown.Message);
    }

    private sealed class DelegatePlugin : BasePlugin
    {
        private readonly Action<PluginContext> _body;

        public DelegatePlugin(Action<PluginContext> body)
        {
            _body = body;
        }

        protected override void ExecutePlugin(PluginContext context) => _body(context);
    }
}
