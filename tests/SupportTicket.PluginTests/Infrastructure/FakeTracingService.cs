using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xrm.Sdk;

namespace SupportTicket.PluginTests.Infrastructure;

/// <summary>Records trace messages so tests can assert on them.</summary>
public sealed class FakeTracingService : ITracingService
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> Messages => _messages;

    public void Trace(string format, params object[] args)
    {
        _messages.Add(args is { Length: > 0 } ? string.Format(CultureInfo.InvariantCulture, format, args) : format);
    }
}
