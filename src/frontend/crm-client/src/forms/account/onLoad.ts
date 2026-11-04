import { FormRules, SaveRules } from "../../rules/index.js";
import { formRules } from "./rules/formRules.js";
import { saveRules } from "./rules/saveRules.js";

/** Applies the rules and registers their OnChange handlers once per form; fixed save messages clear right away. */
export function onLoad(executionContext: Xrm.Events.EventContext): void {
  FormRules.initialize(executionContext.getFormContext(), [...formRules, SaveRules.revalidateOnChange(saveRules)]);
}
