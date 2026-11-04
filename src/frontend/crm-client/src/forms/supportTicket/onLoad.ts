import { FormRules, SaveRules } from "../../rules/index.js";
import { formRules } from "./rules/formRules.js";
import { saveRules } from "./rules/saveRules.js";

/**
 * Rule 5: applies all rules and registers their OnChange handlers once per form. A corrected title
 * also clears its save error right away: Dataverse blocks every save while a field shows an error.
 */
export function onLoad(executionContext: Xrm.Events.EventContext): void {
  FormRules.initialize(executionContext.getFormContext(), [...formRules, SaveRules.revalidateOnChange(saveRules)]);
}
