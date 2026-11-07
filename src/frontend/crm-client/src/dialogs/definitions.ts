// The dialog pages of the app and what they take and return. Form scripts and commands open them with
// Dialogs.open; the pages in crm-pages use the same definitions with useDialog, so both sides agree on
// the types. One definition per folder in crm-pages/src/pages.

import { defineDialog } from "@ogz/crm-ui/protocol";

/** Reopens an inactive support ticket through the ogz_ReopenSupportTicket Custom API, with an optional reason. */
export const ReopenTicketDialog = defineDialog<{ ticketId: string }, { reopenedOn: string }>({
  page: "reopenTicket",
  width: 520,
  height: 420,
});
