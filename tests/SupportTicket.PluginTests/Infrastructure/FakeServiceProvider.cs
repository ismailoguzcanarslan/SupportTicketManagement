using System;
using System.Collections.Generic;

namespace SupportTicket.PluginTests.Infrastructure;

/// <summary>
/// The IServiceProvider passed to IPlugin.Execute. Like the real one, it returns null for
/// services that are not registered.
/// </summary>
public sealed class FakeServiceProvider : IServiceProvider
{
    private readonly Dictionary<Type, object> _services = new();

    public FakeServiceProvider Add<TService>(TService service)
        where TService : class
    {
        _services[typeof(TService)] = service;
        return this;
    }

    public object? GetService(Type serviceType) =>
        _services.TryGetValue(serviceType, out var service) ? service : null;
}
