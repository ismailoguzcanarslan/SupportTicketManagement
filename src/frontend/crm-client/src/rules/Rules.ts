// Built-in rules: generic and parameterized; entity-specific fields, values and messages belong in
// forms/<entity>/rules. A rule whose source column is not on the form does nothing, and a target
// that is not on the form is skipped, so one rule set works on every form of a table. Turn on
// Diagnostics (helpers/diagnostics.ts) to see in the console what was skipped.
//
// Targets:
// - Control rules (visibility, disabled, field notification) take a ControlTarget: a column or
//   control name (every control of that column, body and header), or { tab, section, field } for
//   the controls in one section only.
// - Required level belongs to the column, not to a control, so FieldRequiredRule takes column names.
//
// What a rule does when its condition does not match:
// - Visibility rules own the visibility of their targets: they show on a match and hide otherwise.
// - FieldRequiredRule and FieldDisabledRule only add a constraint: otherwise they restore what the form
//   designer configured. FieldDisabledRule therefore never unlocks a field that is read-only on the form,
//   and never unlocks anything on a read-only form (e.g. an inactive record).
//
// Conflicts are not arbitrated: each rule sets its own state on every change of its column, so two
// rules that set the same state on the same target overwrite each other. Configure one rule per
// target and state (and one id per notification).

import { Control, type ControlTarget, type SectionTarget } from "../helpers/control.js";
import { Diagnostics } from "../helpers/diagnostics.js";
import { Field } from "../helpers/field.js";
import { FormType } from "../helpers/form.js";
import { Section } from "../helpers/tab.js";
import { describeTarget } from "../helpers/target.js";
import { UI } from "../helpers/ui.js";
import { matches, toList, type FormRule, type RuleCondition } from "./FormRule.js";

// Required level and disabled state as the form designer configured them, captured the first time a
// rule changes them, so rules can restore them. Keyed by the column / control object.
const originalRequiredLevels = new WeakMap<object, Xrm.Attributes.RequirementLevel>();
const originalDisabled = new WeakMap<object, boolean>();

function originalRequiredLevel(attribute: Xrm.Attributes.Attribute): Xrm.Attributes.RequirementLevel {
  let level = originalRequiredLevels.get(attribute);
  if (level === undefined) {
    level = attribute.getRequiredLevel();
    originalRequiredLevels.set(attribute, level);
  }
  return level;
}

function originalDisabledState(control: Xrm.Controls.StandardControl): boolean {
  let disabled = originalDisabled.get(control);
  if (disabled === undefined) {
    disabled = control.getDisabled();
    originalDisabled.set(control, disabled);
  }
  return disabled;
}

/** Read-only and disabled forms (e.g. an inactive record): nothing on them may be unlocked. */
const isReadOnlyForm = (formContext: Xrm.FormContext): boolean => {
  const formType = formContext.ui.getFormType();
  return formType === FormType.ReadOnly || formType === FormType.Disabled;
};

function whenFieldPresent<T>(
  rule: string,
  field: string,
  apply: (formContext: Xrm.FormContext, isMatch: boolean) => void,
  condition: RuleCondition<T>,
) {
  return (formContext: Xrm.FormContext): void => {
    if (!Diagnostics.reportMissing(Field.exists(formContext, field), `source column '${field}'`, rule)) return;
    apply(formContext, matches(Field.getValue<T>(formContext, field), condition));
  };
}

const toTargets = (targets: ControlTarget | readonly ControlTarget[]): readonly ControlTarget[] =>
  Array.isArray(targets) ? targets : [targets as ControlTarget];

const reportTarget = (found: boolean, target: ControlTarget, rule: string): boolean =>
  Diagnostics.reportMissing(found, `target '${describeTarget(target)}'`, rule);

/** Shows a section while `field` matches `visibleWhen`, hides it otherwise. */
export function SectionSpecificVisibilityRule<T>(options: SectionTarget & {
  field: string;
  visibleWhen: RuleCondition<T>;
}): FormRule {
  const rule = `SectionSpecificVisibilityRule(${options.field})`;
  return {
    dependsOn: [options.field],
    apply: whenFieldPresent(rule, options.field, (formContext, isMatch) => {
      const found = Section.setVisible(formContext, options.tab, options.section, isMatch);
      Diagnostics.reportMissing(found, `section '${options.tab}/${options.section}'`, rule);
    }, options.visibleWhen),
  };
}

/** Shows the target controls while `field` matches `visibleWhen`, hides them otherwise. */
export function FieldVisibilityRule<T>(options: {
  targets: ControlTarget | readonly ControlTarget[];
  field: string;
  visibleWhen: RuleCondition<T>;
}): FormRule {
  const rule = `FieldVisibilityRule(${options.field})`;
  return {
    dependsOn: [options.field],
    apply: whenFieldPresent(rule, options.field, (formContext, isMatch) => {
      toTargets(options.targets).forEach((target) => reportTarget(Control.setVisible(formContext, target, isMatch), target, rule));
    }, options.visibleWhen),
  };
}

/**
 * Sets the target columns to `level` (default "required") while `field` matches `requiredWhen`,
 * and restores each column's original required level (as configured on the form) otherwise.
 */
export function FieldRequiredRule<T>(options: {
  targets: string | readonly string[];
  field: string;
  requiredWhen: RuleCondition<T>;
  level?: Exclude<Xrm.Attributes.RequirementLevel, "none">;
}): FormRule {
  const level = options.level ?? "required";
  const rule = `FieldRequiredRule(${options.field})`;
  return {
    dependsOn: [options.field],
    apply: whenFieldPresent(rule, options.field, (formContext, isMatch) => {
      for (const target of toList(options.targets)) {
        const attribute = Field.get(formContext, target);
        if (!reportTarget(attribute !== null, target, rule)) continue;
        const original = originalRequiredLevel(attribute!);
        attribute!.setRequiredLevel(isMatch ? level : original);
      }
    }, options.requiredWhen),
  };
}

/**
 * Shows a form notification while `field` matches `showWhen` and clears it otherwise. The `id`
 * makes the notification replaceable and removable; use one id per rule.
 */
export function FormNotificationRule<T>(options: {
  field: string;
  showWhen: RuleCondition<T>;
  message: string;
  id: string;
  level?: Xrm.FormNotificationLevel;
}): FormRule {
  const level = options.level ?? "INFO";
  return {
    dependsOn: [options.field],
    apply: whenFieldPresent(`FormNotificationRule(${options.id})`, options.field, (formContext, isMatch) => {
      if (isMatch) UI.setFormNotification(formContext, options.message, level, options.id);
      else UI.clearFormNotification(formContext, options.id);
    }, options.showWhen),
  };
}

/**
 * Shows a notification on the target control(s) while `field` matches `showWhen`, and clears it
 * otherwise (re-evaluated on every change of `field`).
 *
 * `level` defaults to "RECOMMENDATION" (addNotification): informational, never blocks saving.
 * "ERROR" (setNotification) blocks saving while shown; it is cleared again as soon as `field` no
 * longer matches, but use SaveRules for validation that must stop a save. Use an id that is not
 * used by any other rule (SaveRules use "ogz_save_*").
 */
export function FieldNotificationRule<T>(options: {
  target: ControlTarget;
  field: string;
  showWhen: RuleCondition<T>;
  message: string;
  id: string;
  level?: Xrm.Controls.NotificationLevel;
}): FormRule {
  const level = options.level ?? "RECOMMENDATION";
  const rule = `FieldNotificationRule(${options.id})`;
  return {
    dependsOn: [options.field],
    apply: whenFieldPresent(rule, options.field, (formContext, isMatch) => {
      const found = isMatch
        ? Control.setNotification(formContext, options.target, options.message, options.id, level)
        : Control.clearNotification(formContext, options.target, options.id);
      reportTarget(found, options.target, rule);
    }, options.showWhen),
  };
}

/**
 * Disables the target controls while `field` matches `disabledWhen`. Otherwise each control goes back to
 * the state the form designer gave it, so a field that is read-only on the form stays read-only; on a
 * read-only form (e.g. an inactive record) nothing is ever unlocked.
 */
export function FieldDisabledRule<T>(options: {
  targets: ControlTarget | readonly ControlTarget[];
  field: string;
  disabledWhen: RuleCondition<T>;
}): FormRule {
  const rule = `FieldDisabledRule(${options.field})`;
  return {
    dependsOn: [options.field],
    apply: whenFieldPresent(rule, options.field, (formContext, isMatch) => {
      const readOnlyForm = isReadOnlyForm(formContext);
      for (const target of toTargets(options.targets)) {
        const controls = Control.resolve(formContext, target);
        if (!reportTarget(controls.length > 0, target, rule)) continue;
        for (const control of controls) {
          const original = originalDisabledState(control);
          control.setDisabled(isMatch || original || readOnlyForm);
        }
      }
    }, options.disabledWhen),
  };
}
