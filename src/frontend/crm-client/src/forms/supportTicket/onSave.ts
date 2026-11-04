import { SaveRules } from "../../rules/index.js";
import { saveRules } from "./rules/saveRules.js";

/** Validates before saving; cancels the save and shows the errors while any rule fails. */
export function onSave(executionContext: Xrm.Events.SaveEventContext): void {
  SaveRules.handle(executionContext, saveRules);
}
