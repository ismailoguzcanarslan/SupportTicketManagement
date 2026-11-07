// How helpers find what a target refers to. One target type is accepted everywhere:
//
//   "telephone1"                                          a column / control name: the column and every
//                                                         control of it (body, header, BPF)
//   { tab: "SUMMARY", section: "CONTACT", field: "..." }  only the controls in that section
//
// A value, a requirement level, "is dirty" and the submit mode belong to the column (there is one per
// form, however often it is placed); visibility, disabled, label, focus, notifications and option
// filters belong to a control. A section target therefore finds the column through the control placed
// in that section; it never falls back to controls outside the section.

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

/** The section, or null when the tab or section is not on the form. */
export function resolveSection(formContext: Xrm.FormContext, tab: string, section: string): Xrm.Controls.Section | null {
  return formContext.ui.tabs.get(tab)?.sections.get(section) ?? null;
}

/** The controls a target refers to; empty when it is not on the form. */
export function resolveControls(formContext: Xrm.FormContext, target: ControlTarget): Xrm.Controls.StandardControl[] {
  if (typeof target === "string") {
    const attribute = formContext.getAttribute(target);
    const attributeControls = attribute === null ? [] : (attribute.controls.get() as Xrm.Controls.StandardControl[]);
    if (attributeControls.length > 0) return attributeControls;

    const control = formContext.getControl<Xrm.Controls.StandardControl>(target);
    return control === null ? [] : [control];
  }

  const section = resolveSection(formContext, target.tab, target.section);
  if (section === null) return [];

  // Subgrids, web resources etc. have no bound column; they can only match by control name.
  const controls = section.controls.get() as Partial<Xrm.Controls.StandardControl>[];
  const byName = controls.filter((control) => control.getName?.() === target.field);
  const matching = byName.length > 0 ? byName : controls.filter((control) => control.getAttribute?.()?.getName() === target.field);
  return matching as Xrm.Controls.StandardControl[];
}

/** The column a target refers to; null when it is not on the form (or the control has no column). */
export function resolveAttribute<T extends Xrm.Attributes.Attribute = Xrm.Attributes.Attribute>(
  formContext: Xrm.FormContext,
  target: ControlTarget,
): T | null {
  if (typeof target === "string") {
    const attribute = formContext.getAttribute<T>(target);
    if (attribute !== null) return attribute;
  }
  // A control name such as "header_telephone1", or a control in a section.
  for (const control of resolveControls(formContext, target)) {
    const attribute = (control as Partial<Xrm.Controls.StandardControl>).getAttribute?.();
    if (attribute) return attribute as T;
  }
  return null;
}

/** Readable target for messages, e.g. "telephone1" or "SUMMARY_TAB/CONTACT/telephone1". */
export function describeTarget(target: ControlTarget): string {
  return typeof target === "string" ? target : `${target.tab}/${target.section}/${target.field}`;
}
