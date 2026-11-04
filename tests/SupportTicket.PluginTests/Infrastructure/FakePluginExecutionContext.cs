using System;
using Microsoft.Xrm.Sdk;
using SupportTicket.Base.Registration;

namespace SupportTicket.PluginTests.Infrastructure;

/// <summary>
/// Settable implementation of the plug-in execution context, including the newer
/// IPluginExecutionContext2..7 members so plug-ins may cast to them.
/// </summary>
public sealed class FakePluginExecutionContext : IPluginExecutionContext7
{
    // IExecutionContext
    public int Mode { get; set; } // 0 = synchronous, 1 = asynchronous
    public int IsolationMode { get; set; } = 2; // Sandbox
    public int Depth { get; set; } = 1;
    public string MessageName { get; set; } = string.Empty;
    public string PrimaryEntityName { get; set; } = string.Empty;
    public Guid? RequestId { get; set; }
    public string SecondaryEntityName { get; set; } = "none";
    public ParameterCollection InputParameters { get; } = new ParameterCollection();
    public ParameterCollection OutputParameters { get; } = new ParameterCollection();
    public ParameterCollection SharedVariables { get; } = new ParameterCollection();
    public Guid UserId { get; set; } = Guid.NewGuid();
    public Guid InitiatingUserId { get; set; } = Guid.NewGuid();
    public Guid BusinessUnitId { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; } = Guid.NewGuid();
    public string OrganizationName { get; set; } = "testorg";
    public Guid PrimaryEntityId { get; set; }
    public EntityImageCollection PreEntityImages { get; } = new EntityImageCollection();
    public EntityImageCollection PostEntityImages { get; } = new EntityImageCollection();
    public EntityReference? OwningExtension { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public bool IsExecutingOffline { get; set; }
    public bool IsOfflinePlayback { get; set; }
    public bool IsInTransaction { get; set; } = true;
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public DateTime OperationCreatedOn { get; set; } = DateTime.UtcNow;

    // IPluginExecutionContext
    public int Stage { get; set; } = (int)PluginStage.PreOperation;
    public IPluginExecutionContext? ParentContext { get; set; }

    // IPluginExecutionContext2..7
    public Guid UserAzureActiveDirectoryObjectId { get; set; }
    public Guid InitiatingUserAzureActiveDirectoryObjectId { get; set; }
    public Guid InitiatingUserApplicationId { get; set; }
    public Guid PortalsContactId { get; set; }
    public bool IsPortalsClientCall { get; set; }
    public Guid AuthenticatedUserId { get; set; }
    public EntityImageCollection[] PreEntityImagesCollection { get; set; } = Array.Empty<EntityImageCollection>();
    public EntityImageCollection[] PostEntityImagesCollection { get; set; } = Array.Empty<EntityImageCollection>();
    public string InitiatingUserAgent { get; set; } = string.Empty;
    public string EnvironmentId { get; set; } = string.Empty;
    public Guid TenantId { get; set; }
    public bool IsApplicationUser { get; set; }
}
