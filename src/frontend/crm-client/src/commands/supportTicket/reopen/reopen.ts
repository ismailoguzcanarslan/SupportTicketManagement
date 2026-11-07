// The Reopen Ticket command (Ogz.Commands.SupportTicket.reopen). After the checks it opens the Reopen
// Ticket dialog page (crm-pages, ogz_/pages/reopenTicket.html), which asks for an optional reason and
// calls the ogz_ReopenSupportTicket Custom API: the ticket becomes Active / New, its description is
// cleared and a timeline note records the time, the user and the reason, in one transaction. When the
// dialog reports success the form is refreshed; Cancel or X changes nothing.

import { type ogz_support_ticket, ogz_support_ticketFields as Fields } from "@ogz/crm-entities";
import { ReopenSupportTicketApi } from "../../../constants/customApis.js";
import { ReopenTicketDialog } from "../../../dialogs/definitions.js";
import { type CommandResult, type DialogCommand, type RecordCommandTexts, runRecordCommand } from "../../../helpers/command.js";
import { Dialogs } from "../../../helpers/dialogs.js";
import { EntityService } from "../../../services/EntityService.js";
import { getReopenBlocker, ReopenMessages } from "./validation.js";

export const ReopenTexts = {
  title: "Reopen Ticket",
  notSaved: ReopenMessages.notSaved,
  unsavedChanges: "Save your changes before reopening the ticket.",
  failed: "The ticket could not be reopened.",
} as const satisfies RecordCommandTexts;

const reopenCommand: DialogCommand<ogz_support_ticket> = {
  texts: ReopenTexts,
  read: (id) => EntityService<ogz_support_ticket>(ReopenSupportTicketApi.boundEntity).retrieve(id, [Fields.statecode]),
  check: (stored, id) => getReopenBlocker({ id, statecode: stored.statecode }),
  dialog: (id) => Dialogs.open(ReopenTicketDialog, { ticketId: id }),
};

/** Command bar handler. `primaryControl` is the form's formContext (Command Designer: PrimaryControl). */
export function reopen(primaryControl: Xrm.FormContext): Promise<CommandResult> {
  return runRecordCommand(primaryControl, reopenCommand);
}
