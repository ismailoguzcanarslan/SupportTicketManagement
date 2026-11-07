// The flow every record command bar button shares, so a command only states what is specific to it:
//
//   not saved?  unsaved changes?  -> alert, stop
//   read the stored record -> check it -> alert and stop when it is blocked
//   then either
//     execute:  ask the user -> run it (progress indicator)
//     dialog:   open a dialog page that asks the user and does the work itself (Dialogs.open)
//   -> refresh the form when it was done
//   anything fails -> error dialog
//
// The check runs on the record as stored in Dataverse, because the command writes through the Web API
// (or a Custom API) and does not go through the form's save rules.

import { toErrorMessage, type DialogResult } from "@ogz/crm-ui/protocol";
import { Form } from "./form.js";
import { UI } from "./ui.js";

export interface RecordCommandTexts {
  /** Title of the alert and confirmation dialogs, e.g. "Resolve Ticket". */
  readonly title: string;
  /** Alert for a record that is not saved yet. */
  readonly notSaved: string;
  /** Alert while the form has unsaved changes. */
  readonly unsavedChanges: string;
  /** Question of the confirmation dialog (execute commands). */
  readonly confirm?: string;
  /** Text of the progress indicator (execute commands). */
  readonly progress?: string;
  /** Message of the error dialog; the details show the cause. */
  readonly failed: string;
}

interface RecordCommandBase<TStored> {
  readonly texts: RecordCommandTexts;
  /** Reads what the check needs from the stored record. */
  read(id: string): Promise<TStored>;
  /** null when the command can run; otherwise the reason, shown to the user. */
  check(stored: TStored, id: string): string | null;
}

/** A command that confirms with a simple question and then does the work here. */
export interface ExecuteCommand<TStored> extends RecordCommandBase<TStored> {
  /** Does the work, e.g. an update or a Custom API call. */
  execute(id: string, stored: TStored): Promise<void>;
}

/** A command whose dialog page asks the user and does the work (e.g. calls a Custom API). */
export interface DialogCommand<TStored> extends RecordCommandBase<TStored> {
  /** Opens the dialog, usually `Dialogs.open(definition, { ... })`. */
  dialog(id: string, stored: TStored): Promise<DialogResult<unknown>>;
}

export type RecordCommand<TStored> = ExecuteCommand<TStored> | DialogCommand<TStored>;

/** Outcome of a click, returned for tests and logging; the button ignores it. */
export type CommandResult = "done" | "blocked" | "cancelled" | "failed";

async function runExecute<TStored>(command: ExecuteCommand<TStored>, id: string, stored: TStored): Promise<CommandResult> {
  const { texts } = command;
  if (!(await UI.showConfirm(texts.confirm ?? texts.title, texts.title))) return "cancelled";

  UI.showProgress(texts.progress ?? texts.title);
  try {
    await command.execute(id, stored);
  } finally {
    UI.hideProgress();
  }
  return "done";
}

async function runDialog<TStored>(command: DialogCommand<TStored>, id: string, stored: TStored): Promise<CommandResult> {
  const result = await command.dialog(id, stored);
  if (result.status === "failed") throw new Error(result.error);
  return result.status === "succeeded" ? "done" : "cancelled";
}

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

    const outcome = "dialog" in command ? await runDialog(command, id, stored) : await runExecute(command, id, stored);
    if (outcome === "done") await Form.refresh(formContext);
    return outcome;
  } catch (error) {
    console.error(`${texts.title} failed.`, error);
    await UI.showError(texts.failed, toErrorMessage(error));
    return "failed";
  }
}
