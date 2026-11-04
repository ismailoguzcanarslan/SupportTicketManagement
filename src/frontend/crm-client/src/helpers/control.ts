// Control operations. `name` is a control name; when a column with that name exists, all of its
// controls (body and header) are affected, so a field never stays visible in the header by accident.

import { Field } from "./field.js";
import { Section } from "./tab.js";

/** A section on the form, by the tab and section names set in the form designer. */
export interface SectionTarget {
  readonly tab: string;
  readonly section: string;
}

/**
 * Controls in one section: `field` is a control name (e.g. "telephone1_1") or, when no control in
 * the section has that name, the logical name of the column the controls are bound to.
 */
export interface FieldTarget extends SectionTarget {
  readonly field: string;
}

/** A control name / column name (every matching control on the form) or a control in one section. */
export type ControlTarget = string | FieldTarget;

function resolveControls(formContext: Xrm.FormContext, name: string): Xrm.Controls.StandardControl[] {
  const attributeControls = Field.getControls(formContext, name);
  if (attributeControls.length > 0) return attributeControls;

  const control = formContext.getControl<Xrm.Controls.StandardControl>(name);
  return control === null ? [] : [control];
}

function resolveInSection(formContext: Xrm.FormContext, target: FieldTarget): Xrm.Controls.StandardControl[] {
  const section = Section.get(formContext, target.tab, target.section);
  if (section === null) return [];

  // Subgrids, web resources etc. have no bound column; they can only match by control name.
  const controls = section.controls.get() as Partial<Xrm.Controls.StandardControl>[];
  const byName = controls.filter((control) => control.getName?.() === target.field);
  const matching = byName.length > 0 ? byName : controls.filter((control) => control.getAttribute?.()?.getName() === target.field);
  return matching as Xrm.Controls.StandardControl[];
}

export const Control = {
  /**
   * The controls a target refers to; empty when the tab, section, control or column is not on the
   * form. A structured target never falls back to controls outside its section.
   */
  resolve(formContext: Xrm.FormContext, target: ControlTarget): Xrm.Controls.StandardControl[] {
    return typeof target === "string" ? resolveControls(formContext, target) : resolveInSection(formContext, target);
  },

  /** Readable target for messages, e.g. "telephone1" or "SUMMARY_TAB/CONTACT/telephone1". */
  describe(target: ControlTarget): string {
    return typeof target === "string" ? target : `${target.tab}/${target.section}/${target.field}`;
  },

  get<T extends Xrm.Controls.Control = Xrm.Controls.StandardControl>(formContext: Xrm.FormContext, name: string): T | null {
    return formContext.getControl<T>(name);
  },

  getName(control: Xrm.Controls.Control): string {
    return control.getName();
  },

  /** Returns false when no matching control is on the form. */
  setVisible(formContext: Xrm.FormContext, target: ControlTarget, visible: boolean): boolean {
    const controls = Control.resolve(formContext, target);
    controls.forEach((control) => control.setVisible(visible));
    return controls.length > 0;
  },

  /** Returns false when no matching control is on the form. */
  setDisabled(formContext: Xrm.FormContext, target: ControlTarget, disabled: boolean): boolean {
    const controls = Control.resolve(formContext, target);
    controls.forEach((control) => control.setDisabled(disabled));
    return controls.length > 0;
  },

  /**
   * Shows a notification on the target controls, replacing one with the same id.
   * - "ERROR" uses setNotification: a red error that blocks saving while it is shown.
   * - "RECOMMENDATION" uses addNotification (Unified Interface): informational, never blocks saving.
   *   Controls without addNotification get nothing, rather than a save-blocking error instead.
   * Returns false when no control showed the notification.
   */
  setNotification(
    formContext: Xrm.FormContext,
    target: ControlTarget,
    message: string,
    uniqueId: string,
    level: Xrm.Controls.NotificationLevel = "ERROR",
  ): boolean {
    let shown = 0;
    for (const control of Control.resolve(formContext, target)) {
      if (level === "ERROR") {
        control.setNotification(message, uniqueId);
      } else if (typeof control.addNotification === "function") {
        control.addNotification({ messages: [message], notificationLevel: "RECOMMENDATION", uniqueId });
      } else {
        continue;
      }
      shown++;
    }
    return shown > 0;
  },

  /** Clears the notification with this id (of either level) from the target controls. */
  clearNotification(formContext: Xrm.FormContext, target: ControlTarget, uniqueId: string): boolean {
    const controls = Control.resolve(formContext, target);
    controls.forEach((control) => control.clearNotification(uniqueId));
    return controls.length > 0;
  },

  isVisible(formContext: Xrm.FormContext, name: string): boolean | null {
    return formContext.getControl<Xrm.Controls.StandardControl>(name)?.getVisible() ?? null;
  },

  isDisabled(formContext: Xrm.FormContext, name: string): boolean | null {
    return formContext.getControl<Xrm.Controls.StandardControl>(name)?.getDisabled() ?? null;
  },
} as const;
