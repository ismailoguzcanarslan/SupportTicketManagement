// Tab and section operations. Missing tabs/sections are ignored and reported via the return value.
// Tabs and sections cannot be disabled in the Client API; `setControlsDisabled` disables their controls.

import { resolveSection } from "./target.js";

function setDisabledAll(controls: Xrm.Collection.ItemCollection<Xrm.Controls.Control>, disabled: boolean): void {
  controls.forEach((control) => {
    const standard = control as Partial<Xrm.Controls.StandardControl>;
    standard.setDisabled?.(disabled);
  });
}

/** The columns of the section's controls, in form order, without duplicates (subgrids etc. have none). */
function sectionAttributes(section: Xrm.Controls.Section): Xrm.Attributes.Attribute[] {
  const attributes: Xrm.Attributes.Attribute[] = [];
  section.controls.forEach((control) => {
    const attribute = (control as Partial<Xrm.Controls.StandardControl>).getAttribute?.();
    if (attribute && !attributes.some((known) => known.getName() === attribute.getName())) attributes.push(attribute);
  });
  return attributes;
}

const isEmpty = (value: unknown): boolean =>
  value === null || value === undefined || value === "" || (Array.isArray(value) && value.length === 0);

export const Tab = {
  get(formContext: Xrm.FormContext, tabName: string): Xrm.Controls.Tab | null {
    return formContext.ui.tabs.get(tabName);
  },

  exists(formContext: Xrm.FormContext, tabName: string): boolean {
    return Tab.get(formContext, tabName) !== null;
  },

  setVisible(formContext: Xrm.FormContext, tabName: string, visible: boolean): boolean {
    const tab = Tab.get(formContext, tabName);
    tab?.setVisible(visible);
    return tab !== null;
  },

  isVisible(formContext: Xrm.FormContext, tabName: string): boolean | null {
    return Tab.get(formContext, tabName)?.getVisible() ?? null;
  },

  /** Expands (true) or collapses (false) the tab's content. */
  setExpanded(formContext: Xrm.FormContext, tabName: string, expanded: boolean): boolean {
    const tab = Tab.get(formContext, tabName);
    tab?.setDisplayState(expanded ? "expanded" : "collapsed");
    return tab !== null;
  },

  /** Opens the tab, as if the user selected it. */
  setFocus(formContext: Xrm.FormContext, tabName: string): boolean {
    const tab = Tab.get(formContext, tabName);
    tab?.setFocus();
    return tab !== null;
  },

  /** Disables or enables every control in every section of the tab. */
  setControlsDisabled(formContext: Xrm.FormContext, tabName: string, disabled: boolean): boolean {
    const tab = Tab.get(formContext, tabName);
    tab?.sections.forEach((section) => setDisabledAll(section.controls, disabled));
    return tab !== null;
  },
} as const;

export const Section = {
  get(formContext: Xrm.FormContext, tabName: string, sectionName: string): Xrm.Controls.Section | null {
    return resolveSection(formContext, tabName, sectionName);
  },

  exists(formContext: Xrm.FormContext, tabName: string, sectionName: string): boolean {
    return Section.get(formContext, tabName, sectionName) !== null;
  },

  setVisible(formContext: Xrm.FormContext, tabName: string, sectionName: string, visible: boolean): boolean {
    const section = Section.get(formContext, tabName, sectionName);
    section?.setVisible(visible);
    return section !== null;
  },

  isVisible(formContext: Xrm.FormContext, tabName: string, sectionName: string): boolean | null {
    return Section.get(formContext, tabName, sectionName)?.getVisible() ?? null;
  },

  /** Disables or enables every control in the section. */
  setControlsDisabled(formContext: Xrm.FormContext, tabName: string, sectionName: string, disabled: boolean): boolean {
    const section = Section.get(formContext, tabName, sectionName);
    if (section !== null) setDisabledAll(section.controls, disabled);
    return section !== null;
  },

  /** Logical names of the columns placed in the section, in form order; empty when it is not on the form. */
  getFields(formContext: Xrm.FormContext, tabName: string, sectionName: string): string[] {
    const section = Section.get(formContext, tabName, sectionName);
    return section === null ? [] : sectionAttributes(section).map((attribute) => attribute.getName());
  },

  /** Values of the section's columns by logical name, e.g. { telephone1: "+90 ...", emailaddress1: null }. */
  getValues(formContext: Xrm.FormContext, tabName: string, sectionName: string): Record<string, unknown> {
    const section = Section.get(formContext, tabName, sectionName);
    if (section === null) return {};
    return Object.fromEntries(sectionAttributes(section).map((attribute) => [attribute.getName(), attribute.getValue() ?? null]));
  },

  /**
   * Sets the requirement level of every column placed in the section. The level belongs to the column,
   * so it also applies where the same column is placed elsewhere on the form. `except` leaves columns as
   * they are.
   */
  setRequiredLevelAll(
    formContext: Xrm.FormContext,
    tabName: string,
    sectionName: string,
    level: Xrm.Attributes.RequirementLevel,
    except: readonly string[] = [],
  ): boolean {
    const section = Section.get(formContext, tabName, sectionName);
    if (section === null) return false;
    sectionAttributes(section)
      .filter((attribute) => !except.includes(attribute.getName()))
      .forEach((attribute) => attribute.setRequiredLevel(level));
    return true;
  },

  /** Logical names of the section's business-required columns that are empty. */
  getMissingRequired(formContext: Xrm.FormContext, tabName: string, sectionName: string): string[] {
    const section = Section.get(formContext, tabName, sectionName);
    if (section === null) return [];
    return sectionAttributes(section)
      .filter((attribute) => attribute.getRequiredLevel() === "required" && isEmpty(attribute.getValue()))
      .map((attribute) => attribute.getName());
  },
} as const;
