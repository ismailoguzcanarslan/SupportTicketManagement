// The Resolve Ticket command (Ogz.Commands.SupportTicket.resolve). The shared flow (saved? unsaved
// changes? check, confirm, run, refresh, errors) is runRecordCommand; this file only says what to read,
// what to check and what to write.
//
// "Resolved" is an Inactive status reason, so statecode and statuscode are written together; Dataverse
// rejects a Resolved status reason on an Active record.

import {
  type ogz_support_ticket,
  ogz_support_ticketFields as Fields,
  ogz_support_ticket_statecode as State,
  ogz_support_ticket_statuscode as Status,
} from "@ogz/crm-entities";
import { type CommandResult, type RecordCommand, type RecordCommandTexts, runRecordCommand } from "../../../helpers/command.js";
import { EntityService } from "../../../services/EntityService.js";
import { getResolveBlocker, ResolveMessages } from "./validation.js";

export const ResolveTexts = {
  title: "Resolve Ticket",
  notSaved: ResolveMessages.notSaved,
  unsavedChanges: "Save your changes before resolving the ticket.",
  confirm: "Are you sure you want to resolve this ticket?",
  progress: "Resolving the ticket...",
  failed: "The ticket could not be resolved.",
} as const satisfies RecordCommandTexts;

/** The columns written to resolve a ticket. */
export const RESOLVED: Pick<ogz_support_ticket, "statecode" | "statuscode"> = {
  statecode: State.Inactive,
  statuscode: Status.Resolved,
};

const tickets = () => EntityService<ogz_support_ticket>("ogz_support_ticket");

const resolveCommand: RecordCommand<ogz_support_ticket> = {
  texts: ResolveTexts,
  read: (id) => tickets().retrieve(id, [Fields.statecode, Fields.ogz_description]),
  check: (stored, id) => getResolveBlocker({ id, statecode: stored.statecode, description: stored.ogz_description }),
  execute: (id) => tickets().update(id, RESOLVED),
};

/** Command bar handler. `primaryControl` is the form's formContext (Command Designer: PrimaryControl). */
export function resolve(primaryControl: Xrm.FormContext): Promise<CommandResult> {
  return runRecordCommand(primaryControl, resolveCommand);
}
