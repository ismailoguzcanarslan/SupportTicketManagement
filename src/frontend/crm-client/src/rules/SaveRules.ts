// Runs SaveRules in the form's OnSave event: shows one notification per error, clears notifications
// of errors that are gone, and cancels the save (preventDefault) while any rule of severity "error"
// fails. Rules of severity "info" only show their messages; the save goes on.
//
// Two Dataverse behaviours shape this:
// - An error notification on a control (setNotification) blocks the save by itself, before OnSave
//   handlers can clear it. So errors of a rule's columns are also re-checked, and cleared once
//   valid, when those columns change (revalidateOnChange); otherwise a corrected value could never
//   be saved.
// - Each event may get a different formContext object for the same form, so the notifications shown
//   are tracked by notification id, never by formContext identity.

import { Control } from "../helpers/control.js";
import { Field } from "../helpers/field.js";
import { UI } from "../helpers/ui.js";
import type { FormRule } from "./FormRule.js";
import type { SaveRule, SaveRuleSeverity, SaveValidationError } from "./SaveRule.js";

const NOTIFICATION_PREFIX = "ogz_save_";

interface ShownNotification {
  readonly ruleId: string;
  readonly field?: string;
}

/** Error of one rule, with the notification id it is shown under. */
export interface RuleError extends SaveValidationError {
  readonly ruleId: string;
  readonly notificationId: string;
  readonly severity: SaveRuleSeverity;
}

export interface SaveValidationOutcome {
  /** False while any rule of severity "error" fails; "info" messages do not make the form invalid. */
  readonly isValid: boolean;
  /** Every message, of both severities. */
  readonly errors: readonly RuleError[];
}

// Notifications currently shown, by notification id. One form runs in a script frame at a time, and
// clearing an id that a form does not show is a no-op, so this needs no form key.
const shown = new Map<string, ShownNotification>();

function unexpectedError(rule: SaveRule, error: unknown): SaveValidationError {
  // A broken rule must never let invalid data through silently: it blocks the save and says so.
  console.error(`Save rule '${rule.id}' threw an error`, error);
  const detail = error instanceof Error ? error.message : String(error);
  return { message: `The record could not be validated (rule '${rule.id}': ${detail}). The save was stopped; please contact your administrator.` };
}

function clear(formContext: Xrm.FormContext, id: string, notification: ShownNotification): void {
  UI.clearFormNotification(formContext, id);
  if (notification.field) Field.clearNotification(formContext, notification.field, id);
  shown.delete(id);
}

export const SaveRules = {
  /** Evaluates every rule (even after a failure) and collects all errors. Does not touch the UI. */
  validate(formContext: Xrm.FormContext, rules: readonly SaveRule[]): SaveValidationOutcome {
    const errors: RuleError[] = [];

    for (const rule of rules) {
      let ruleErrors: readonly SaveValidationError[];
      let severity: SaveRuleSeverity = rule.severity ?? "error";
      try {
        const result = rule.validate(formContext);
        ruleErrors = result.isValid ? [] : result.errors.length > 0 ? result.errors : [{ message: `Validation '${rule.id}' failed.` }];
      } catch (error) {
        ruleErrors = [unexpectedError(rule, error)];
        severity = "error"; // even an "info" rule must not let a broken check pass silently
      }

      ruleErrors.forEach((error, index) =>
        errors.push({ ...error, ruleId: rule.id, severity, notificationId: `${NOTIFICATION_PREFIX}${rule.id}${index === 0 ? "" : `_${index}`}` }));
    }

    return { isValid: !errors.some((e) => e.severity === "error"), errors };
  },

  /**
   * OnSave handler body: validates, updates notifications and calls preventDefault() when any rule
   * of severity "error" fails. Returns true when the save may continue.
   */
  handle(executionContext: Xrm.Events.SaveEventContext, rules: readonly SaveRule[]): boolean {
    const formContext = executionContext.getFormContext();
    const outcome = SaveRules.validate(formContext, rules);

    SaveRules.showNotifications(formContext, outcome.errors);
    if (!outcome.isValid) executionContext.getEventArgs().preventDefault();
    return outcome.isValid;
  },

  /** Shows each error once (same id replaces) and clears notifications of errors that are gone. */
  showNotifications(formContext: Xrm.FormContext, errors: readonly RuleError[]): void {
    const current = new Set(errors.map((e) => e.notificationId));
    for (const [id, notification] of [...shown]) {
      if (!current.has(id)) clear(formContext, id, notification);
    }

    for (const error of errors) {
      if (error.severity === "error") {
        UI.setFormNotification(formContext, error.message, "ERROR", error.notificationId);
        if (error.field) Field.setNotification(formContext, error.field, error.message, error.notificationId);
      } else {
        // Never setNotification here: a control error blocks the save, which "info" must not do.
        UI.setFormNotification(formContext, error.message, "INFO", error.notificationId);
        if (error.field) Control.setNotification(formContext, error.field, error.message, error.notificationId, "RECOMMENDATION");
      }
      shown.set(error.notificationId, error.field ? { ruleId: error.ruleId, field: error.field } : { ruleId: error.ruleId });
    }
  },

  /**
   * Clears the shown messages of every rule that passes now. Rules that still fail keep theirs
   * (OnSave shows the current message); nothing new is shown while the user is still typing.
   */
  clearResolved(formContext: Xrm.FormContext, rules: readonly SaveRule[]): void {
    for (const rule of rules) {
      const ruleNotifications = [...shown].filter(([, n]) => n.ruleId === rule.id);
      // Any message counts, not only errors: an "info" rule that still fails keeps its message.
      if (ruleNotifications.length === 0 || SaveRules.validate(formContext, [rule]).errors.length > 0) continue;
      for (const [id, notification] of ruleNotifications) clear(formContext, id, notification);
    }
  },

  /**
   * A FormRule (for FormRules.initialize in OnLoad) that re-checks the save rules when one of their
   * columns changes and clears errors that are fixed, so the field notification no longer blocks the
   * next save. OnSave still validates everything.
   */
  revalidateOnChange(rules: readonly SaveRule[]): FormRule {
    return {
      dependsOn: [...new Set(rules.flatMap((rule) => rule.fields ?? []))],
      apply: (formContext) => SaveRules.clearResolved(formContext, rules),
    };
  },
} as const;
