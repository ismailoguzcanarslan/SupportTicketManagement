// Control operations: visibility, disabled state, label, focus, notifications, choice options and lookup
// filters. Every function takes a target (see target.ts): a column/control name affects every control of
// that column (body and header), so a field never stays visible in the header by accident; a
// { tab, section, field } target affects only the control in that section.

import { type ControlTarget, describeTarget, resolveControls, type SectionTarget, type FieldTarget } from "./target.js";

export type { ControlTarget, FieldTarget, SectionTarget };

type LookupControl = Xrm.Controls.LookupControl;
type OptionSetControl = Xrm.Controls.OptionSetControl;

// Current custom filter of each lookup control, read by one PreSearch handler per control. Keyed by the
// control object, so entries disappear with the form.
const lookupFilters = new WeakMap<object, { filter: string; entityType: string | undefined } | null>();

function hasMethod<K extends string>(control: object, method: K): control is Record<K, (...args: never[]) => unknown> {
  return typeof (control as Record<string, unknown>)[method] === "function";
}

/** The choice controls of the target, each with the full option list of its column. */
function choiceControls(formContext: Xrm.FormContext, target: ControlTarget): { control: OptionSetControl; options: Xrm.OptionSetValue[] }[] {
  return resolveControls(formContext, target)
    .filter((control) => hasMethod(control, "clearOptions") && hasMethod(control, "addOption"))
    // A choice control is always bound to a choice column, which knows all of its options.
    .map((control) => ({
      control: control as unknown as OptionSetControl,
      options: (control.getAttribute() as Xrm.Attributes.OptionSetAttribute).getOptions(),
    }));
}

export const Control = {
  /**
   * The controls a target refers to; empty when the tab, section, control or column is not on the
   * form. A structured target never falls back to controls outside its section.
   */
  resolve(formContext: Xrm.FormContext, target: ControlTarget): Xrm.Controls.StandardControl[] {
    return resolveControls(formContext, target);
  },

  /** Readable target for messages, e.g. "telephone1" or "SUMMARY_TAB/CONTACT/telephone1". */
  describe(target: ControlTarget): string {
    return describeTarget(target);
  },

  get<T extends Xrm.Controls.Control = Xrm.Controls.StandardControl>(formContext: Xrm.FormContext, name: string): T | null {
    return formContext.getControl<T>(name);
  },

  getName(control: Xrm.Controls.Control): string {
    return control.getName();
  },

  /** Returns false when no matching control is on the form. */
  setVisible(formContext: Xrm.FormContext, target: ControlTarget, visible: boolean): boolean {
    const controls = resolveControls(formContext, target);
    controls.forEach((control) => control.setVisible(visible));
    return controls.length > 0;
  },

  /** Returns false when no matching control is on the form. */
  setDisabled(formContext: Xrm.FormContext, target: ControlTarget, disabled: boolean): boolean {
    const controls = resolveControls(formContext, target);
    controls.forEach((control) => control.setDisabled(disabled));
    return controls.length > 0;
  },

  /** Visibility of the first matching control; null when none is on the form. */
  isVisible(formContext: Xrm.FormContext, target: ControlTarget): boolean | null {
    return resolveControls(formContext, target)[0]?.getVisible() ?? null;
  },

  /** Disabled state of the first matching control; null when none is on the form. */
  isDisabled(formContext: Xrm.FormContext, target: ControlTarget): boolean | null {
    return resolveControls(formContext, target)[0]?.getDisabled() ?? null;
  },

  /** Label of the first matching control; null when none is on the form. */
  getLabel(formContext: Xrm.FormContext, target: ControlTarget): string | null {
    return resolveControls(formContext, target)[0]?.getLabel() ?? null;
  },

  /** Changes the label of the matching controls (until the form reloads). */
  setLabel(formContext: Xrm.FormContext, target: ControlTarget, label: string): boolean {
    const controls = resolveControls(formContext, target);
    controls.forEach((control) => control.setLabel(label));
    return controls.length > 0;
  },

  /** Moves the cursor to the first visible matching control (opening its tab). */
  setFocus(formContext: Xrm.FormContext, target: ControlTarget): boolean {
    const control = resolveControls(formContext, target).find((candidate) => candidate.getVisible());
    control?.setFocus();
    return control !== undefined;
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
    for (const control of resolveControls(formContext, target)) {
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
    const controls = resolveControls(formContext, target);
    controls.forEach((control) => control.clearNotification(uniqueId));
    return controls.length > 0;
  },

  /**
   * Choice columns: offers only the given values in the target controls, in the column's order. It only
   * changes what the user can pick; the current value is not changed. Call again with other values to
   * change the list, or use resetOptions to offer every value again. Returns false when no choice control
   * matched.
   */
  setOptions(formContext: Xrm.FormContext, target: ControlTarget, values: readonly number[]): boolean {
    const controls = choiceControls(formContext, target);
    for (const { control, options } of controls) {
      control.clearOptions();
      options.filter((option) => values.includes(option.value)).forEach((option) => control.addOption(option));
    }
    return controls.length > 0;
  },

  /** Choice columns: removes the given values from the target controls. */
  removeOptions(formContext: Xrm.FormContext, target: ControlTarget, values: readonly number[]): boolean {
    const controls = choiceControls(formContext, target);
    controls.forEach(({ control }) => values.forEach((value) => control.removeOption(value)));
    return controls.length > 0;
  },

  /** Choice columns: offers every value of the column again. */
  resetOptions(formContext: Xrm.FormContext, target: ControlTarget): boolean {
    const controls = choiceControls(formContext, target);
    for (const { control, options } of controls) {
      control.clearOptions();
      options.forEach((option) => control.addOption(option));
    }
    return controls.length > 0;
  },

  /**
   * Lookup columns: limits the records offered when the user searches, with a FetchXML <filter> such as
   * `<filter><condition attribute="statecode" operator="eq" value="0" /></filter>`. `entityType` names the
   * table the filter applies to (needed when the lookup offers several tables). Calling it again replaces
   * the filter; null removes it. One PreSearch handler is registered per control, however often this runs.
   */
  setLookupFilter(formContext: Xrm.FormContext, target: ControlTarget, filter: string | null, entityType?: string): boolean {
    const controls = resolveControls(formContext, target).filter((control) => hasMethod(control, "addPreSearch"));
    for (const control of controls as unknown as LookupControl[]) {
      const registered = lookupFilters.has(control);
      lookupFilters.set(control, filter === null ? null : { filter, entityType });
      if (registered) continue;

      control.addPreSearch(() => {
        const current = lookupFilters.get(control);
        if (current) control.addCustomFilter(current.filter, current.entityType);
      });
    }
    return controls.length > 0;
  },

  /** Lookup columns: the view shown when the user searches, by its id. */
  setLookupDefaultView(formContext: Xrm.FormContext, target: ControlTarget, viewId: string): boolean {
    const controls = resolveControls(formContext, target).filter((control) => hasMethod(control, "setDefaultView"));
    (controls as unknown as LookupControl[]).forEach((control) => control.setDefaultView(viewId));
    return controls.length > 0;
  },
} as const;
