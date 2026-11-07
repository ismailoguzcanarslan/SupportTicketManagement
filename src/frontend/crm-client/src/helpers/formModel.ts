// A fluent view of a form for handler code: pick a column, or a tab and section, once and work on it.
//
//   const form = FormModel(formContext);
//   form.field(Fields.ogz_priority).getText();                         // "Critical"
//   const contact = form.tab("SUMMARY_TAB").section("CONTACT");
//   contact.field("telephone1").setRequired(true).setLabel("Mobile").setVisible(true);
//   contact.getMissingRequired();                                      // ["telephone1"]
//
// It adds no behavior of its own: every call goes to Field, Control, Tab, Section or Grid, so both styles
// give the same result and the rule engine can keep using those helpers directly. Like them, it tolerates
// columns, tabs and sections that are not on the form: setters do nothing (check with exists()), getters
// return null. Setters return the same object, so calls can be chained.

import { Control } from "./control.js";
import { Field } from "./field.js";
import { Grid } from "./grid.js";
import { Section, Tab } from "./tab.js";
import type { ControlTarget } from "./target.js";

export interface FieldModel<T = unknown> {
  /** What this model refers to: a column name or { tab, section, field }. */
  readonly target: ControlTarget;
  /** True when the column is on the form (in the section, for a section field). */
  exists(): boolean;
  /** Logical name of the column, e.g. of the control placed in the section. */
  logicalName(): string | null;

  getValue(): T | null;
  /** Display text, e.g. the label of a choice. */
  getText(): string | null;
  setValue(value: T | null, options?: { fireOnChange?: boolean }): FieldModel<T>;
  clear(options?: { fireOnChange?: boolean }): FieldModel<T>;
  fireOnChange(): FieldModel<T>;
  isDirty(): boolean | null;
  setSubmitMode(mode: Xrm.SubmitMode): FieldModel<T>;

  /** Requirement level of the column (it applies wherever the column is placed). */
  setRequired(required: boolean): FieldModel<T>;
  setRequiredLevel(level: Xrm.Attributes.RequirementLevel): FieldModel<T>;
  isRequired(): boolean | null;

  setVisible(visible: boolean): FieldModel<T>;
  isVisible(): boolean | null;
  setDisabled(disabled: boolean): FieldModel<T>;
  isDisabled(): boolean | null;
  setLabel(label: string): FieldModel<T>;
  getLabel(): string | null;
  focus(): FieldModel<T>;

  /** "ERROR" (default) blocks saving while shown; "RECOMMENDATION" never does. */
  notify(message: string, uniqueId: string, level?: Xrm.Controls.NotificationLevel): FieldModel<T>;
  clearNotification(uniqueId: string): FieldModel<T>;

  /** Choice columns: offer only these values / remove these values / offer all again. */
  setOptions(values: readonly number[]): FieldModel<T>;
  removeOptions(values: readonly number[]): FieldModel<T>;
  resetOptions(): FieldModel<T>;

  /** Lookup columns: FetchXML <filter> for the search (null removes it), and the default view. */
  setLookupFilter(filter: string | null, entityType?: string): FieldModel<T>;
  setLookupDefaultView(viewId: string): FieldModel<T>;
}

export interface SectionModel {
  readonly tab: string;
  readonly name: string;
  exists(): boolean;
  /** A column placed in this section, by control name or column logical name. */
  field<T = unknown>(name: string): FieldModel<T>;
  /** Logical names of the columns placed in this section. */
  fields(): string[];
  /** Values of the columns placed in this section, by logical name. */
  getValues(): Record<string, unknown>;
  /** Required (true) or optional (false) for every column placed here, except the given ones. */
  setRequiredAll(required: boolean, except?: readonly string[]): SectionModel;
  /** Business-required columns of this section that are empty. */
  getMissingRequired(): string[];
  setVisible(visible: boolean): SectionModel;
  isVisible(): boolean | null;
  setControlsDisabled(disabled: boolean): SectionModel;
}

export interface TabModel {
  readonly name: string;
  exists(): boolean;
  section(name: string): SectionModel;
  setVisible(visible: boolean): TabModel;
  isVisible(): boolean | null;
  expand(): TabModel;
  collapse(): TabModel;
  /** Opens the tab, as if the user selected it. */
  focus(): TabModel;
  setControlsDisabled(disabled: boolean): TabModel;
}

export interface GridModel {
  readonly name: string;
  exists(): boolean;
  refresh(): GridModel;
  getRecordCount(): number | null;
}

export interface FormModel {
  readonly formContext: Xrm.FormContext;
  /** A column by logical name (all its controls), or a control by name, e.g. "header_telephone1". */
  field<T = unknown>(name: string): FieldModel<T>;
  tab(name: string): TabModel;
  /** Shortcut for tab(tab).section(section). */
  section(tab: string, section: string): SectionModel;
  grid(name: string): GridModel;
}

function fieldModel<T>(formContext: Xrm.FormContext, target: ControlTarget): FieldModel<T> {
  const model: FieldModel<T> = {
    target,
    exists: () => Field.exists(formContext, target),
    logicalName: () => Field.getLogicalName(formContext, target),

    getValue: () => Field.getValue<T>(formContext, target),
    getText: () => Field.getText(formContext, target),
    setValue: (value, options) => {
      Field.setValue(formContext, target, value as Xrm.Attributes.AttributeValues | null, options);
      return model;
    },
    clear: (options) => {
      Field.clear(formContext, target, options);
      return model;
    },
    fireOnChange: () => {
      Field.fireOnChange(formContext, target);
      return model;
    },
    isDirty: () => Field.isDirty(formContext, target),
    setSubmitMode: (mode) => {
      Field.setSubmitMode(formContext, target, mode);
      return model;
    },

    setRequired: (required) => {
      Field.setRequired(formContext, target, required);
      return model;
    },
    setRequiredLevel: (level) => {
      Field.setRequiredLevel(formContext, target, level);
      return model;
    },
    isRequired: () => Field.isRequired(formContext, target),

    setVisible: (visible) => {
      Control.setVisible(formContext, target, visible);
      return model;
    },
    isVisible: () => Control.isVisible(formContext, target),
    setDisabled: (disabled) => {
      Control.setDisabled(formContext, target, disabled);
      return model;
    },
    isDisabled: () => Control.isDisabled(formContext, target),
    setLabel: (label) => {
      Control.setLabel(formContext, target, label);
      return model;
    },
    getLabel: () => Control.getLabel(formContext, target),
    focus: () => {
      Control.setFocus(formContext, target);
      return model;
    },

    notify: (message, uniqueId, level) => {
      Control.setNotification(formContext, target, message, uniqueId, level);
      return model;
    },
    clearNotification: (uniqueId) => {
      Control.clearNotification(formContext, target, uniqueId);
      return model;
    },

    setOptions: (values) => {
      Control.setOptions(formContext, target, values);
      return model;
    },
    removeOptions: (values) => {
      Control.removeOptions(formContext, target, values);
      return model;
    },
    resetOptions: () => {
      Control.resetOptions(formContext, target);
      return model;
    },

    setLookupFilter: (filter, entityType) => {
      Control.setLookupFilter(formContext, target, filter, entityType);
      return model;
    },
    setLookupDefaultView: (viewId) => {
      Control.setLookupDefaultView(formContext, target, viewId);
      return model;
    },
  };
  return model;
}

function sectionModel(formContext: Xrm.FormContext, tab: string, name: string): SectionModel {
  const model: SectionModel = {
    tab,
    name,
    exists: () => Section.exists(formContext, tab, name),
    field: <T>(field: string) => fieldModel<T>(formContext, { tab, section: name, field }),
    fields: () => Section.getFields(formContext, tab, name),
    getValues: () => Section.getValues(formContext, tab, name),
    setRequiredAll: (required, except) => {
      Section.setRequiredLevelAll(formContext, tab, name, required ? "required" : "none", except);
      return model;
    },
    getMissingRequired: () => Section.getMissingRequired(formContext, tab, name),
    setVisible: (visible) => {
      Section.setVisible(formContext, tab, name, visible);
      return model;
    },
    isVisible: () => Section.isVisible(formContext, tab, name),
    setControlsDisabled: (disabled) => {
      Section.setControlsDisabled(formContext, tab, name, disabled);
      return model;
    },
  };
  return model;
}

function tabModel(formContext: Xrm.FormContext, name: string): TabModel {
  const model: TabModel = {
    name,
    exists: () => Tab.exists(formContext, name),
    section: (section) => sectionModel(formContext, name, section),
    setVisible: (visible) => {
      Tab.setVisible(formContext, name, visible);
      return model;
    },
    isVisible: () => Tab.isVisible(formContext, name),
    expand: () => {
      Tab.setExpanded(formContext, name, true);
      return model;
    },
    collapse: () => {
      Tab.setExpanded(formContext, name, false);
      return model;
    },
    focus: () => {
      Tab.setFocus(formContext, name);
      return model;
    },
    setControlsDisabled: (disabled) => {
      Tab.setControlsDisabled(formContext, name, disabled);
      return model;
    },
  };
  return model;
}

function gridModel(formContext: Xrm.FormContext, name: string): GridModel {
  const model: GridModel = {
    name,
    exists: () => Grid.get(formContext, name) !== null,
    refresh: () => {
      Grid.refresh(formContext, name);
      return model;
    },
    getRecordCount: () => Grid.getRecordCount(formContext, name),
  };
  return model;
}

/** A fluent view of the form; cheap to create, so make one per handler call. */
export function FormModel(formContext: Xrm.FormContext): FormModel {
  return {
    formContext,
    field: <T>(name: string) => fieldModel<T>(formContext, name),
    tab: (name) => tabModel(formContext, name),
    section: (tab, section) => sectionModel(formContext, tab, section),
    grid: (name) => gridModel(formContext, name),
  };
}
