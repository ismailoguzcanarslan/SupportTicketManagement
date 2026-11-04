namespace SupportTicket.Base.Registration;

/// <summary>Event pipeline stage of an SDK message processing step (sdkmessageprocessingstep.stage).</summary>
public enum PluginStage
{
    PreValidation = 10,
    PreOperation = 20,
    PostOperation = 40,
}
