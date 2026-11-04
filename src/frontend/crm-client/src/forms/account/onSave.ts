import { SaveRules } from "../../rules/index.js";
import { saveRules } from "./rules/saveRules.js";

/** Runs the save checks; only checks of severity "error" can cancel the save (Account has none). */
export function onSave(executionContext: Xrm.Events.SaveEventContext): void {
  SaveRules.handle(executionContext, saveRules);
}
