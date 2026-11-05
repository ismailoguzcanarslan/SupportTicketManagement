// The Resolve Ticket command (Ogz.Commands.SupportTicket.resolve):
//   unsaved changes? -> read the stored record -> check it -> ask the user -> update -> refresh the form
//
// "Resolved" is an Inactive status reason, so statecode and statuscode are written together; Dataverse
// rejects a Resolved status reason on an Active record.

import {
  type ogz_support_ticket,
  ogz_support_ticketFields as Fields,
  ogz_support_ticket_statecode as State,
  ogz_support_ticket_statuscode as Status,
} from "@ogz/crm-entities";
import { Form, UI } from "../../../helpers/XrmHelper.js";
import { EntityService } from "../../../services/EntityService.js";
import { getResolveBlocker, ResolveMessages } from "./validation.js";

export const ResolveTexts = {
  unsavedChanges: "Save your changes before resolving the ticket.",
  confirm: "Are you sure you want to resolve this ticket?",
  confirmTitle: "Resolve Ticket",
  progress: "Resolving the ticket...",
  failed: "The ticket could not be resolved.",
} as const;

/** The columns written to resolve a ticket. */
export const RESOLVED: Pick<ogz_support_ticket, "statecode" | "statuscode"> = {
  statecode: State.Inactive,
  statuscode: Status.Resolved,
};

const tickets = () => EntityService<ogz_support_ticket>("ogz_support_ticket");

/** Outcome of a click, returned for tests and logging; the button ignores it. */
export type ResolveResult = "resolved" | "blocked" | "cancelled" | "failed";

/** Command bar handler. `primaryControl` is the form's formContext (Command Designer: PrimaryControl). */
export async function resolve(primaryControl: Xrm.FormContext): Promise<ResolveResult> {
  const id = Form.getEntityId(primaryControl);
  if (id === null) return block(ResolveMessages.notSaved);
  // The check reads the stored record; changes still on the form would not be part of it.
  if (primaryControl.data.entity.getIsDirty()) return block(ResolveTexts.unsavedChanges);

  try {
    const stored = await tickets().retrieve(id, [Fields.statecode, Fields.ogz_description]);
    const blocker = getResolveBlocker({ id, statecode: stored.statecode, description: stored.ogz_description });
    if (blocker !== null) return block(blocker);

    if (!(await UI.showConfirm(ResolveTexts.confirm, ResolveTexts.confirmTitle))) return "cancelled";

    UI.showProgress(ResolveTexts.progress);
    try {
      await tickets().update(id, RESOLVED);
    } finally {
      UI.hideProgress();
    }
    await Form.refresh(primaryControl);
    return "resolved";
  } catch (error) {
    console.error("Resolve Ticket failed.", error);
    await UI.showError(ResolveTexts.failed, error instanceof Error ? error.message : String(error));
    return "failed";
  }
}

async function block(message: string): Promise<ResolveResult> {
  await UI.showAlert(message, ResolveTexts.confirmTitle);
  return "blocked";
}
