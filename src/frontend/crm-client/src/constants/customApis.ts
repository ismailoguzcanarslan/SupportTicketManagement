// Names of the Custom APIs the client calls. The same names are declared on the server side
// (SupportTicket.Plugins, e.g. ReopenSupportTicketApi.cs); keep both in step.

export const ReopenSupportTicketApi = {
  name: "ogz_ReopenSupportTicket",
  boundEntity: "ogz_support_ticket",
  parameters: { reason: "Reason" },
  response: { reopenedOn: "ReopenedOn" },
} as const;
