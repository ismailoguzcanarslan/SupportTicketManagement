// Support Ticket UI rules (OnLoad and the OnChange of the columns they depend on).

import { ogz_support_ticket_statuscode, ogz_support_ticketFields, ogz_support_ticket_ogz_priority } from "@ogz/crm-entities";
import { FieldRequiredRule, FormNotificationRule, FieldNotificationRule, isEmpty, type FormRule } from "../../../rules/index.js";

export const CATEGORY_NOTIFICATION_ID = "ogz_support_ticket_category_missing";

/** Status reasons that close a ticket and therefore need a description. */
const CLOSING_STATUSES: readonly ogz_support_ticket_statuscode[] = [
  ogz_support_ticket_statuscode.Resolved,
  ogz_support_ticket_statuscode.Cancelled,
];

export const formRules: readonly FormRule[] = [
  // Rules 1 and 4: ask for a category while none is selected; re-evaluated on every category change.
  FormNotificationRule({
    field: ogz_support_ticketFields.ogz_category,
    showWhen: isEmpty,
    message: "Select a ticket category so the ticket can be routed to the right team.",
    level: "INFO",
    id: CATEGORY_NOTIFICATION_ID,
  }),
  // Rules 2 and 3: a resolved or cancelled ticket needs a description. For every other status the
  // column goes back to the required level configured on the form.
  FieldRequiredRule({
    targets: ogz_support_ticketFields.ogz_description,
    field: ogz_support_ticketFields.statuscode,
    requiredWhen: CLOSING_STATUSES,
  }),

  // Critical tickets: remind the user to describe the problem in detail (does not block saving).
  FieldNotificationRule({
    field: ogz_support_ticketFields.ogz_priority,
    showWhen: ogz_support_ticket_ogz_priority.Critical,
    target: ogz_support_ticketFields.ogz_description,
    message: "Critical tickets need a detailed description.",
    id: "ogz_support_ticket_critical_description_hint",
  }),
];
