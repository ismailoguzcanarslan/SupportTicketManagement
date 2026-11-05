// The flow every record command bar button shares, so a command only states what is specific to it:
//
//   not saved?  unsaved changes?  -> alert, stop
//   read the stored record -> check it -> alert and stop when it is blocked
//   ask the user -> run it (progress indicator) -> refresh the form
//   anything fails -> error dialog
//
// The check runs on the record as stored in Dataverse, because the command writes through the Web API
// (or a Custom API) and does not go through the form's save rules.

import { Form } from "./form.js";
import { UI } from "./ui.js";

export interface RecordCommandTexts {
  /** Title of the alert and confirmation dialogs, e.g. "Resolve Ticket". */
  readonly title: string;
  /** Alert for a record that is not saved yet. */
  readonly notSaved: string;
  /** Alert while the form has unsaved changes. */
  readonly unsavedChanges: string;
  /** Question of the confirmation dialog. */
  readonly confirm: string;
  /** Text of the progress indicator. */
  readonly progress: string;
  /** Message of the error dialog; the details show the cause. */
  readonly failed: string;
}

export interface RecordCommand<TStored> {
  readonly texts: RecordCommandTexts;
  /** Reads what the check needs from the stored record. */
  read(id: string): Promise<TStored>;
  /** null when the command can run; otherwise the reason, shown to the user. */
  check(stored: TStored, id: string): string | null;
  /** Does the work, e.g. an update or a Custom API call. */
  execute(id: string, stored: TStored): Promise<void>;
}

/** Outcome of a click, returned for tests and logging; the button ignores it. */
export type CommandResult = "done" | "blocked" | "cancelled" | "failed";

export async function runRecordCommand<TStored>(formContext: Xrm.FormContext, command: RecordCommand<TStored>): Promise<CommandResult> {
  const { texts } = command;
  const block = async (message: string): Promise<CommandResult> => {
    await UI.showAlert(message, texts.title);
    return "blocked";
  };

  const id = Form.getEntityId(formContext);
  if (id === null) return block(texts.notSaved);
  // The check reads the stored record; changes still on the form would not be part of it.
  if (formContext.data.entity.getIsDirty()) return block(texts.unsavedChanges);

  try {
    const stored = await command.read(id);
    const blocker = command.check(stored, id);
    if (blocker !== null) return block(blocker);

    if (!(await UI.showConfirm(texts.confirm, texts.title))) return "cancelled";

    UI.showProgress(texts.progress);
    try {
      await command.execute(id, stored);
    } finally {
      UI.hideProgress();
    }
    await Form.refresh(formContext);
    return "done";
  } catch (error) {
    console.error(`${texts.title} failed.`, error);
    await UI.showError(texts.failed, errorMessage(error));
    return "failed";
  }
}

/** Error, Web API error object ({ message }) or anything else, as text. */
function errorMessage(error: unknown): string {
  if (error instanceof Error) return error.message;
  if (typeof error === "object" && error !== null && typeof (error as { message?: unknown }).message === "string") {
    return (error as { message: string }).message;
  }
  return String(error);
}
