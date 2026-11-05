namespace SupportTicket.Plugins.SupportTickets;

/// <summary>
/// Names of the ogz_ReopenSupportTicket Custom API, shared by its plug-in, the tests and the registration.
/// The frontend uses the same names (crm-client src/constants/customApis.ts).
/// </summary>
/// <remarks>
/// The Custom API itself is created in the maker portal (solution OguzLabsSupportTicketManagement):
/// unique name ogz_ReopenSupportTicket, binding type Entity, bound entity ogz_support_ticket, not a function,
/// allowed custom processing step type None. Request parameter Reason (String, optional).
/// Response property ReopenedOn (DateTime). DeploymentHelper links this plug-in as its main handler.
/// </remarks>
public static class ReopenSupportTicketApi
{
    public const string Name = "ogz_ReopenSupportTicket";

    /// <summary>Request parameters.</summary>
    public static class Parameters
    {
        /// <summary>Optional text added to the timeline note.</summary>
        public const string Reason = "Reason";
    }

    /// <summary>Response properties.</summary>
    public static class Response
    {
        /// <summary>When the ticket was reopened (UTC).</summary>
        public const string ReopenedOn = "ReopenedOn";
    }
}
