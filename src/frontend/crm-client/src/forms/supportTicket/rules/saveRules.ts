// Support Ticket save validation (OnSave). Server-side validation (SupportTicketValidationPlugin)
// still applies; these checks only give earlier feedback.

import { ogz_support_ticketFields } from "@ogz/crm-entities";
import { MinTextLengthRule, type SaveRule } from "../../../rules/index.js";

export const saveRules: readonly SaveRule[] = [
  // A title needs at least 3 characters after trimming; this also rejects whitespace-only titles,
  // so the title gets one message for both cases. An empty title is left to the column's required
  // validation and the server-side plug-in.
  MinTextLengthRule({
    field: ogz_support_ticketFields.ogz_ticket_title,
    minLength: 3,
    message: "Title must contain at least 3 characters.",
  }),
];
