// Runs a rule set on a form: applies every rule on load and whenever the form data loads again (after a
// save or refresh), and re-applies the affected rules when a column they depend on changes.

import { Event } from "../helpers/event.js";
import { Form } from "../helpers/form.js";
import { normalizeId } from "../helpers/xrm.js";
import type { FormRule } from "./FormRule.js";

/** Rule sets of an entity's forms: one per form id, and the set for every other form. */
export interface RuleSetsByForm<T> {
  readonly default: readonly T[];
  /** Keyed by form id (with or without braces, any case). */
  readonly byFormId?: Readonly<Record<string, readonly T[]>>;
}

/**
 * The rule set registered for the form being shown, or the default set. Forms without a form
 * selector (e.g. quick create) and unknown form ids get the default set.
 */
export function selectRuleSet<T>(formContext: Xrm.FormContext, sets: RuleSetsByForm<T>): readonly T[] {
  const formId = Form.getFormId(formContext);
  if (formId === null || sets.byFormId === undefined) return sets.default;

  const match = Object.entries(sets.byFormId).find(([id]) => normalizeId(id) === formId);
  return match?.[1] ?? sets.default;
}

// One active rule set per form; initializing again replaces it instead of stacking handlers.
const active = new WeakMap<object, () => void>();

function applySafely(rule: FormRule, formContext: Xrm.FormContext): void {
  try {
    rule.apply(formContext);
  } catch (error) {
    // A broken rule must not stop the other rules or the form from loading.
    console.error("Form rule failed", error);
  }
}

export const FormRules = {
  /**
   * Applies `rules` now and on every change of the columns they depend on.
   * Returns a function that removes the OnChange handlers again.
   */
  initialize(formContext: Xrm.FormContext, rules: readonly FormRule[]): () => void {
    active.get(formContext.data.entity)?.();

    const byColumn = new Map<string, FormRule[]>();
    for (const rule of rules) {
      for (const column of rule.dependsOn) {
        byColumn.set(column, [...(byColumn.get(column) ?? []), rule]);
      }
    }

    const handlers = new Map<string, Xrm.Events.Attribute.ChangeEventHandler>();
    for (const [column, columnRules] of byColumn) {
      const handler: Xrm.Events.Attribute.ChangeEventHandler = (context) => {
        const changedForm = context.getFormContext();
        columnRules.forEach((rule) => applySafely(rule, changedForm));
      };
      if (Event.registerOnChange(formContext, column, handler)) handlers.set(column, handler);
    }

    rules.forEach((rule) => applySafely(rule, formContext));

    // The form OnLoad event does not run again after a save (e.g. a new record becoming an existing one)
    // or a refresh, but the data and the form state change; apply every rule again then.
    const onDataLoad: Xrm.Events.DataLoadEventHandler = (context) => {
      const loadedForm = context.getFormContext();
      rules.forEach((rule) => applySafely(rule, loadedForm));
    };
    Event.registerOnDataLoad(formContext, onDataLoad);

    const dispose = (): void => {
      handlers.forEach((handler, column) => Event.unregisterOnChange(formContext, column, handler));
      handlers.clear();
      Event.unregisterOnDataLoad(formContext, onDataLoad);
      active.delete(formContext.data.entity);
    };
    active.set(formContext.data.entity, dispose);
    return dispose;
  },
} as const;
