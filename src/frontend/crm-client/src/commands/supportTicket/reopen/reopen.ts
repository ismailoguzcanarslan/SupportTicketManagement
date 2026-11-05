// The Reopen Ticket command (Ogz.Commands.SupportTicket.reopen). The work happens on the server: the
// ogz_ReopenSupportTicket Custom API sets the ticket Active / New, clears its description and adds a
// timeline note with the time and the user, in one transaction. The button only checks, asks and calls it.

import { type ogz_support_ticket, ogz_support_ticketFields as Fields } from "@ogz/crm-entities";
import { ReopenSupportTicketApi } from "../../../constants/customApis.js";
import { type CommandResult, type RecordCommand, type RecordCommandTexts, runRecordCommand } from "../../../helpers/command.js";
import { executeCustomApi } from "../../../services/CustomApiService.js";
import { EntityService } from "../../../services/EntityService.js";
import { getReopenBlocker, ReopenMessages } from "./validation.js";

export const ReopenTexts = {
  title: "Reopen Ticket",
  notSaved: ReopenMessages.notSaved,
  unsavedChanges: "Save your changes before reopening the ticket.",
  confirm: "Are you sure you want to reopen this ticket? Its description will be cleared.",
  progress: "Reopening the ticket...",
  failed: "The ticket could not be reopened.",
} as const satisfies RecordCommandTexts;

const reopenCommand: RecordCommand<ogz_support_ticket> = {
  texts: ReopenTexts,
  read: (id) => EntityService<ogz_support_ticket>(ReopenSupportTicketApi.boundEntity).retrieve(id, [Fields.statecode]),
  check: (stored, id) => getReopenBlocker({ id, statecode: stored.statecode }),
  execute: async (id) => {
    await executeCustomApi({ name: ReopenSupportTicketApi.name, boundTo: { entityName: ReopenSupportTicketApi.boundEntity, id } });
  },
};

/** Command bar handler. `primaryControl` is the form's formContext (Command Designer: PrimaryControl). */
export function reopen(primaryControl: Xrm.FormContext): Promise<CommandResult> {
  return runRecordCommand(primaryControl, reopenCommand);
}
