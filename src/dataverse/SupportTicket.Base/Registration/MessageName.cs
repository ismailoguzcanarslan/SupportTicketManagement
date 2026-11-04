namespace SupportTicket.Base.Registration;

/// <summary>
/// Names of common Dataverse SDK messages (sdkmessage.name). Constants rather than an enum so
/// attributes can also take Custom API / Custom Action names as plain strings.
/// </summary>
public static class MessageName
{
    public const string Assign = "Assign";
    public const string Associate = "Associate";
    public const string Create = "Create";
    public const string Delete = "Delete";
    public const string Disassociate = "Disassociate";
    public const string Retrieve = "Retrieve";
    public const string RetrieveMultiple = "RetrieveMultiple";
    public const string SetState = "SetState";
    public const string Update = "Update";
}
