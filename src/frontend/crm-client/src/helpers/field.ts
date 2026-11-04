// Attribute (column) operations. Every function tolerates attributes that are not on the form and
// reports whether it did anything, so form scripts work on every form of a table.

export const Field = {
  get<T extends Xrm.Attributes.Attribute = Xrm.Attributes.Attribute>(formContext: Xrm.FormContext, name: string): T | null {
    return formContext.getAttribute<T>(name);
  },

  exists(formContext: Xrm.FormContext, name: string): boolean {
    return formContext.getAttribute(name) !== null;
  },

  /** Current value, or null when the column is empty or not on the form. */
  getValue<T = unknown>(formContext: Xrm.FormContext, name: string): T | null {
    const attribute = formContext.getAttribute(name);
    return attribute === null ? null : (attribute.getValue() as T | null);
  },

  /** Sets a value. Returns false when the column is not on the form. */
  setValue<T extends Xrm.Attributes.AttributeValues>(
    formContext: Xrm.FormContext,
    name: string,
    value: T | null,
    options: { fireOnChange?: boolean } = {},
  ): boolean {
    const attribute = formContext.getAttribute<Xrm.Attributes.Attribute<T>>(name);
    if (attribute === null) return false;

    attribute.setValue(value);
    if (options.fireOnChange === true) attribute.fireOnChange();
    return true;
  },

  getName(attribute: Xrm.Attributes.Attribute): string {
    return attribute.getName();
  },

  getRequiredLevel(formContext: Xrm.FormContext, name: string): Xrm.Attributes.RequirementLevel | null {
    return formContext.getAttribute(name)?.getRequiredLevel() ?? null;
  },

  setRequiredLevel(formContext: Xrm.FormContext, name: string, level: Xrm.Attributes.RequirementLevel): boolean {
    const attribute = formContext.getAttribute(name);
    if (attribute === null) return false;

    attribute.setRequiredLevel(level);
    return true;
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
