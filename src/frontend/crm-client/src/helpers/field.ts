// Column (attribute) operations: value, requirement level, dirty state, submit mode. Every function
// takes a column name or a { tab, section, field } target (see target.ts), tolerates columns that are not
// on the form and reports whether it did anything, so form scripts work on every form of a table.
//
// These belong to the column, not to one placement of it: a column placed in two sections has one
// value and one requirement level. A section target only says which column is meant.

import { type ControlTarget, resolveAttribute } from "./target.js";

/** Column to work on: its logical name, or the column of a control in a given tab and section. */
export type FieldRef = ControlTarget;

export const Field = {
  get<T extends Xrm.Attributes.Attribute = Xrm.Attributes.Attribute>(formContext: Xrm.FormContext, field: FieldRef): T | null {
    return resolveAttribute<T>(formContext, field);
  },

  exists(formContext: Xrm.FormContext, field: FieldRef): boolean {
    return resolveAttribute(formContext, field) !== null;
  },

  /** Logical name of the column a target refers to, e.g. the column of a control in a section. */
  getLogicalName(formContext: Xrm.FormContext, field: FieldRef): string | null {
    return resolveAttribute(formContext, field)?.getName() ?? null;
  },

  /** Current value, or null when the column is empty or not on the form. */
  getValue<T = unknown>(formContext: Xrm.FormContext, field: FieldRef): T | null {
    const attribute = resolveAttribute(formContext, field);
    return attribute === null ? null : (attribute.getValue() as T | null);
  },

  /**
   * Display text of the value: the label of a choice ("Critical"), the labels of a multi-select choice
   * joined with "; ", the names of a lookup's records, otherwise the value as text ("true" for a two-option
   * column, ISO text for a date). Null when empty.
   */
  getText(formContext: Xrm.FormContext, field: FieldRef): string | null {
    const attribute = resolveAttribute(formContext, field);
    if (attribute === null) return null;

    const value = attribute.getValue() as unknown;
    if (value === null || value === undefined) return null;

    const withText = attribute as Partial<{ getText(): string | string[] | null }>;
    if (typeof withText.getText === "function") {
      const text = withText.getText();
      return Array.isArray(text) ? text.join("; ") : text;
    }
    if (Array.isArray(value)) return value.map((item: { name?: string }) => item.name ?? "").join("; ");
    return value instanceof Date ? value.toISOString() : String(value);
  },

  /** Sets a value. Returns false when the column is not on the form. */
  setValue<T extends Xrm.Attributes.AttributeValues>(
    formContext: Xrm.FormContext,
    field: FieldRef,
    value: T | null,
    options: { fireOnChange?: boolean } = {},
  ): boolean {
    const attribute = resolveAttribute<Xrm.Attributes.Attribute<T>>(formContext, field);
    if (attribute === null) return false;

    attribute.setValue(value);
    if (options.fireOnChange === true) attribute.fireOnChange();
    return true;
  },

  /** Empties the column. Returns false when the column is not on the form. */
  clear(formContext: Xrm.FormContext, field: FieldRef, options: { fireOnChange?: boolean } = {}): boolean {
    return Field.setValue(formContext, field, null, options);
  },

  /** Runs the column's OnChange handlers, e.g. after a value was set by code. */
  fireOnChange(formContext: Xrm.FormContext, field: FieldRef): boolean {
    const attribute = resolveAttribute(formContext, field);
    attribute?.fireOnChange();
    return attribute !== null;
  },

  /** True when the value was changed since the form loaded or was saved; null when not on the form. */
  isDirty(formContext: Xrm.FormContext, field: FieldRef): boolean | null {
    return resolveAttribute(formContext, field)?.getIsDirty() ?? null;
  },

  /**
   * Whether the value is sent when the record is saved: "dirty" (default: only when changed), "always"
   * (e.g. a disabled column set by code) or "never".
   */
  setSubmitMode(formContext: Xrm.FormContext, field: FieldRef, mode: Xrm.SubmitMode): boolean {
    const attribute = resolveAttribute(formContext, field);
    attribute?.setSubmitMode(mode);
    return attribute !== null;
  },

  getName(attribute: Xrm.Attributes.Attribute): string {
    return attribute.getName();
  },

  getRequiredLevel(formContext: Xrm.FormContext, field: FieldRef): Xrm.Attributes.RequirementLevel | null {
    return resolveAttribute(formContext, field)?.getRequiredLevel() ?? null;
  },

  setRequiredLevel(formContext: Xrm.FormContext, field: FieldRef, level: Xrm.Attributes.RequirementLevel): boolean {
    const attribute = resolveAttribute(formContext, field);
    attribute?.setRequiredLevel(level);
    return attribute !== null;
  },

  /** Business required (true) or optional (false). */
  setRequired(formContext: Xrm.FormContext, field: FieldRef, required: boolean): boolean {
    return Field.setRequiredLevel(formContext, field, required ? "required" : "none");
  },

  /** True when business required; null when the column is not on the form. */
  isRequired(formContext: Xrm.FormContext, field: FieldRef): boolean | null {
    const level = Field.getRequiredLevel(formContext, field);
    return level === null ? null : level === "required";
  },

  /** Shows a notification on every control of the column (body and header). */
  setNotification(formContext: Xrm.FormContext, name: string, message: string, uniqueId: string): boolean {
    const controls = Field.getControls(formContext, name);
    controls.forEach((control) => control.setNotification(message, uniqueId));
    return controls.length > 0;
  },

  clearNotification(formContext: Xrm.FormContext, name: string, uniqueId?: string): boolean {
    const controls = Field.getControls(formContext, name);
    controls.forEach((control) => control.clearNotification(uniqueId));
    return controls.length > 0;
  },

  /** All controls bound to the column, e.g. "telephone1" and "header_telephone1". */
  getControls(formContext: Xrm.FormContext, name: string): Xrm.Controls.StandardControl[] {
    const attribute = formContext.getAttribute(name);
    return attribute === null ? [] : (attribute.controls.get() as Xrm.Controls.StandardControl[]);
  },
} as const;
