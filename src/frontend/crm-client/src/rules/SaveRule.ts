// Declarative OnSave validation: rules that check the form state and may block the save.
// Unlike FormRule (UI changes on load/change), a SaveRule only reports; SaveRules acts on the result.

import { Field } from "../helpers/field.js";
import { isEmpty, matches, type RuleCondition } from "./FormRule.js";

/**
 * "error" (default) blocks the save. "info" only informs: a form notification of level INFO and a
 * RECOMMENDATION next to the field (addNotification), neither of which blocks saving.
 */
export type SaveRuleSeverity = "error" | "info";

export interface SaveValidationError {
  /** Column the error belongs to; its controls get a field notification. */
  readonly field?: string;
  readonly message: string;
}

export interface SaveValidationResult {
  readonly isValid: boolean;
  readonly errors: readonly SaveValidationError[];
}

export interface SaveRule {
  /** Stable id; used to replace and clear this rule's notifications. */
  readonly id: string;

  /** Columns the rule checks; a change of one re-checks it and clears its errors once fixed. */
  readonly fields?: readonly string[];

  /** Default "error": a failing rule blocks the save. "info" shows its messages and lets the save go on. */
  readonly severity?: SaveRuleSeverity;

  /** Checks the current form state. Must be synchronous and must not change the form. */
  validate(formContext: Xrm.FormContext): SaveValidationResult;
}

export const valid = (): SaveValidationResult => ({ isValid: true, errors: [] });

export const invalid = (...errors: SaveValidationError[]): SaveValidationResult => ({ isValid: false, errors });

/** Label of the column's first control, falling back to the column name. */
function fieldLabel(formContext: Xrm.FormContext, field: string): string {
  return Field.getControls(formContext, field)[0]?.getLabel() || field;
}

/**
 * Blocks saving when a text column has fewer than `minLength` characters after trimming leading and
 * trailing whitespace. Whitespace-only text therefore fails too, so don't combine it with
 * NotWhitespaceRule on the same column. An empty column passes: whether a value is required stays
 * Dataverse's (and the form's) job. A column that is not on the form passes too.
 */
export function MinTextLengthRule(options: { field: string; minLength: number; message?: string; id?: string }): SaveRule {
  if (!Number.isInteger(options.minLength) || options.minLength < 1) {
    throw new RangeError(`MinTextLengthRule for '${options.field}': minLength must be a positive integer.`);
  }

  return {
    id: options.id ?? `${options.field}_min_length`,
    fields: [options.field],
    validate(formContext) {
      const value = Field.getValue<unknown>(formContext, options.field);
      if (typeof value !== "string" || value.length === 0) return valid();

      // Count characters (code points), so e.g. an emoji counts once.
      if (Array.from(value.trim()).length >= options.minLength) return valid();

      return invalid({
        field: options.field,
        message: options.message ?? `${fieldLabel(formContext, options.field)} must contain at least ${options.minLength} characters.`,
      });
    },
  };
}

/**
 * Reports an empty `field` while `whenField` matches `when`, e.g. "a phone number is needed while
 * phone is the preferred contact method". With severity "info" it only reminds the user. A column
 * that is not on the form passes, so the rule never reports something the user cannot fix.
 */
export function RequiredWhenRule<T>(options: {
  field: string;
  whenField: string;
  when: RuleCondition<T>;
  message: string;
  id?: string;
  severity?: SaveRuleSeverity;
}): SaveRule {
  return {
    id: options.id ?? `${options.field}_required_when_${options.whenField}`,
    fields: [options.field, options.whenField],
    ...(options.severity === undefined ? {} : { severity: options.severity }),
    validate(formContext) {
      if (!Field.exists(formContext, options.field) || !Field.exists(formContext, options.whenField)) return valid();
      if (!matches(Field.getValue<T>(formContext, options.whenField), options.when)) return valid();
      if (!isEmpty(Field.getValue(formContext, options.field))) return valid();

      return invalid({ field: options.field, message: options.message });
    },
  };
}

/**
 * Blocks saving when a text column contains only whitespace (spaces, tabs, line breaks).
 * An empty column passes: whether a value is required stays Dataverse's (and the form's) job.
 * A column that is not on the form passes too.
 */
export function NotWhitespaceRule(options: { field: string; message?: string; id?: string }): SaveRule {
  return {
    id: options.id ?? `${options.field}_not_whitespace`,
    fields: [options.field],
    validate(formContext) {
      const value = Field.getValue<unknown>(formContext, options.field);
      if (typeof value !== "string" || value.length === 0 || value.trim().length > 0) return valid();

      return invalid({
        field: options.field,
        message: options.message ?? `${fieldLabel(formContext, options.field)} cannot contain only spaces.`,
      });
    },
  };
}
