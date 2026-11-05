// "Can this ticket be reopened?" as a pure check. The ogz_ReopenSupportTicket Custom API checks the same
// rule on the server (SupportTicketReopenLogic); this copy lets the button explain the reason before
// calling it.

import { ogz_support_ticket_statecode as State } from "@ogz/crm-entities";

export interface ReopenCandidate {
  /** Record id; null for a ticket that is not saved yet. */
  readonly id: string | null;
  readonly statecode: number | null | undefined;
}

export const ReopenMessages = {
  notSaved: "Save the ticket before reopening it.",
  notInactive: "Only inactive tickets can be reopened.",
} as const;

/** null when the ticket can be reopened (any inactive status reason); otherwise the reason, for the user. */
export function getReopenBlocker(ticket: ReopenCandidate): string | null {
  if (!ticket.id) return ReopenMessages.notSaved;
  if (ticket.statecode !== State.Inactive) return ReopenMessages.notInactive;
  return null;
}
