using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk;

namespace SupportTicket.PluginTests.Infrastructure;

/// <summary>Returns the same organization service for every caller and records who asked for it.</summary>
public sealed class FakeOrganizationServiceFactory : IOrganizationServiceFactory
{
    private readonly IOrganizationService _service;
    private readonly List<Guid?> _requestedUserIds = new();

    public FakeOrganizationServiceFactory(IOrganizationService service)
    {
        _service = service;
    }

    /// <summary>User ids passed to CreateOrganizationService (null = SYSTEM user).</summary>
    public IReadOnlyList<Guid?> RequestedUserIds => _requestedUserIds;

    public IOrganizationService CreateOrganizationService(Guid? userId)
    {
        _requestedUserIds.Add(userId);
        return _service;
    }
}
