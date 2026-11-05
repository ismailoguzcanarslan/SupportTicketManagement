// "Can this ticket be resolved?" as a pure check: no Xrm, no form, only the values it is given. The
// command runs it on the record as stored in Dataverse, because it writes through the Web API and does
// not go through the form's save rules.

import { ogz_support_ticket_statecode as State } from "@ogz/crm-entities";

/** What the check needs to know about a ticket. */
export interface ResolveCandidate {
  /** Record id; null for a ticket that is not saved yet. */
  readonly id: string | null;
  readonly statecode: number | null | undefined;
  readonly description: string | null | undefined;
}

export const ResolveMessages = {
  notSaved: "Save the ticket before resolving it.",
  notActive: "Only active tickets can be resolved.",
  noDescription: "Add a description before resolving the ticket.",
} as const;

/** null when the ticket can be resolved; otherwise the reason, for the user. Checked in this order. */
export function getResolveBlocker(ticket: ResolveCandidate): string | null {
  if (!ticket.id) return ResolveMessages.notSaved;
  if (ticket.statecode !== State.Active) return ResolveMessages.notActive;
  if (!ticket.description?.trim()) return ResolveMessages.noDescription;
  return null;
}
