// Tab and section operations. Missing tabs/sections are ignored and reported via the return value.
// Tabs and sections cannot be disabled in the Client API; `setControlsDisabled` disables their controls.

function setDisabledAll(controls: Xrm.Collection.ItemCollection<Xrm.Controls.Control>, disabled: boolean): void {
  controls.forEach((control) => {
    const standard = control as Partial<Xrm.Controls.StandardControl>;
    standard.setDisabled?.(disabled);
  });
}

export const Tab = {
  get(formContext: Xrm.FormContext, tabName: string): Xrm.Controls.Tab | null {
    return formContext.ui.tabs.get(tabName);
  },

  setVisible(formContext: Xrm.FormContext, tabName: string, visible: boolean): boolean {
    const tab = Tab.get(formContext, tabName);
    tab?.setVisible(visible);
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
    return Tab.get(formContext, tabName)?.sections.get(sectionName) ?? null;
  },

  setVisible(formContext: Xrm.FormContext, tabName: string, sectionName: string, visible: boolean): boolean {
    const section = Section.get(formContext, tabName, sectionName);
    section?.setVisible(visible);
    return section !== null;
  },

  /** Disables or enables every control in the section. */
  setControlsDisabled(formContext: Xrm.FormContext, tabName: string, sectionName: string, disabled: boolean): boolean {
    const section = Section.get(formContext, tabName, sectionName);
    if (section !== null) setDisabledAll(section.controls, disabled);
    return section !== null;
  },
} as const;
