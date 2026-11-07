// Reusable, stateful test doubles for the Dataverse form Client API.
//
// Every method is a `vi.fn`, so tests can assert calls with `vi.mocked(...)` or override behavior
// with `mockReturnValue(...)`. Objects are returned typed as the real `Xrm` interfaces so code
// under test receives what it expects; only the members listed here are implemented.

import { vi } from "vitest";

const EMPTY_GUID = "00000000-0000-0000-0000-000000000000";

/** XrmEnum.FormType values (ambient const enums are not usable with isolatedModules). */
export const FormType = {
  Undefined: 0,
  Create: 1,
  Update: 2,
  ReadOnly: 3,
  Disabled: 4,
  BulkEdit: 6,
} as const;

// ---------------------------------------------------------------------------------------------
// Collections
// ---------------------------------------------------------------------------------------------

export function createItemCollectionMock<T>(
  items: readonly T[],
  getName: (item: T) => string,
): Xrm.Collection.ItemCollection<T> {
  function get(arg?: string | number | Xrm.Collection.MatchingDelegate<T>): T | T[] | null {
    if (arg === undefined) return [...items];
    if (typeof arg === "number") return items[arg] ?? null;
    if (typeof arg === "string") return items.find((item) => getName(item) === arg) ?? null;
    return items.filter((item, index) => arg(item, index));
  }

  return {
    get: vi.fn(get),
    forEach: vi.fn((delegate: Xrm.Collection.IterativeDelegate<T>) => items.forEach((item, index) => delegate(item, index))),
    getLength: vi.fn(() => items.length),
  } as unknown as Xrm.Collection.ItemCollection<T>;
}

// ---------------------------------------------------------------------------------------------
// Attributes
// ---------------------------------------------------------------------------------------------

export interface AttributeMockOptions {
  value?: unknown;
  attributeType?: Xrm.Attributes.AttributeType;
  requiredLevel?: Xrm.Attributes.RequirementLevel;
  submitMode?: Xrm.SubmitMode;
  /** Label of the control created for the attribute on the form (default: the column name). */
  label?: string;
  /** Choice columns: the options of the column. Adds getOptions/getText and option methods on its controls. */
  options?: Xrm.OptionSetValue[];
  /** Options of the control created for the attribute on the form, e.g. { lookup: true }. */
  control?: Omit<ControlMockOptions, "attribute" | "label">;
}

// Lets an attribute build the event context for its OnChange handlers once it joins a form.
const attributeOwners = new WeakMap<object, Xrm.FormContext>();
// Live backing array of each attribute's `controls` collection, filled when it joins a form.
const attributeControls = new WeakMap<object, Xrm.Controls.StandardControl[]>();

export function createAttributeMock(name: string, options: AttributeMockOptions = {}): Xrm.Attributes.Attribute {
  let value: unknown = options.value ?? null;
  let requiredLevel: Xrm.Attributes.RequirementLevel = options.requiredLevel ?? "none";
  let submitMode: Xrm.SubmitMode = options.submitMode ?? "dirty";
  let isDirty = false;
  const handlers: Xrm.Events.ContextSensitiveHandler[] = [];
  const controls: Xrm.Controls.StandardControl[] = [];

  const attribute = {
    getName: vi.fn(() => name),
    getAttributeType: vi.fn(() => options.attributeType ?? "string"),
    getValue: vi.fn(() => value),
    setValue: vi.fn((newValue: unknown) => {
      value = newValue;
      isDirty = true;
    }),
    getIsDirty: vi.fn(() => isDirty),
    getRequiredLevel: vi.fn(() => requiredLevel),
    setRequiredLevel: vi.fn((level: Xrm.Attributes.RequirementLevel) => {
      requiredLevel = level;
    }),
    getSubmitMode: vi.fn(() => submitMode),
    setSubmitMode: vi.fn((mode: Xrm.SubmitMode) => {
      submitMode = mode;
    }),
    addOnChange: vi.fn((handler: Xrm.Events.ContextSensitiveHandler) => {
      handlers.push(handler);
    }),
    removeOnChange: vi.fn((handler: Xrm.Events.ContextSensitiveHandler) => {
      const index = handlers.indexOf(handler);
      if (index >= 0) handlers.splice(index, 1);
    }),
    fireOnChange: vi.fn(() => {
      const formContext = attributeOwners.get(attribute);
      if (!formContext) throw new Error(`Attribute '${name}' is not part of a form; create it via createFormContextMock.`);
      const eventContext = createEventContextMock(formContext, { eventSource: attribute as unknown as Xrm.Attributes.Attribute });
      for (const handler of [...handlers]) handler(eventContext);
    }),
    controls: createItemCollectionMock(controls, (control) => control.getName()),
    ...(options.options === undefined
      ? {}
      : {
          getOptions: vi.fn(() => [...options.options!]),
          getText: vi.fn(() => {
            const label = (selected: unknown) => options.options!.find((option) => option.value === selected)?.text ?? "";
            if (value === null) return null;
            return Array.isArray(value) ? value.map(label) : label(value);
          }),
        }),
  };

  attributeControls.set(attribute, controls);
  return attribute as unknown as Xrm.Attributes.Attribute;
}

// ---------------------------------------------------------------------------------------------
// Controls
// ---------------------------------------------------------------------------------------------

export interface ControlMockOptions {
  attribute?: Xrm.Attributes.Attribute;
  controlType?: Xrm.Controls.ControlType;
  label?: string;
  visible?: boolean;
  disabled?: boolean;
  /** False simulates a control without addNotification (only setNotification). Default true. */
  addNotification?: boolean;
  /** Adds the lookup control methods (PreSearch, custom filter, default view). */
  lookup?: boolean;
  /** Adds the subgrid control methods; getGrid().getTotalRecordCount() returns recordCount. */
  grid?: { recordCount: number };
}

/** Option methods for a control of a choice column; `offeredOptions` is the list the user can pick from. */
function choiceControlMembers(attribute: Xrm.Attributes.Attribute | undefined): object {
  const getOptions = (attribute as Partial<Xrm.Attributes.OptionSetAttribute> | undefined)?.getOptions;
  if (getOptions === undefined) return {};

  const offeredOptions: Xrm.OptionSetValue[] = [...getOptions()];
  return {
    clearOptions: vi.fn(() => {
      offeredOptions.length = 0;
    }),
    addOption: vi.fn((option: Xrm.OptionSetValue) => {
      offeredOptions.push(option);
    }),
    removeOption: vi.fn((value: number) => {
      const index = offeredOptions.findIndex((option) => option.value === value);
      if (index >= 0) offeredOptions.splice(index, 1);
    }),
    /** Test helper (not part of the Client API): the options the user can pick from. */
    offeredOptions,
  };
}

/**
 * Lookup control methods. `runPreSearch()` (test helper) simulates the user opening the search: it runs
 * the PreSearch handlers and returns the custom filters they added.
 */
function lookupControlMembers(): object {
  const preSearchHandlers: (() => void)[] = [];
  const customFilters: { filter: string; entityType: string | undefined }[] = [];
  return {
    addPreSearch: vi.fn((handler: () => void) => {
      preSearchHandlers.push(handler);
    }),
    addCustomFilter: vi.fn((filter: string, entityType?: string) => {
      customFilters.push({ filter, entityType });
    }),
    setDefaultView: vi.fn(),
    preSearchHandlers,
    runPreSearch: () => {
      customFilters.length = 0;
      preSearchHandlers.forEach((handler) => handler());
      return [...customFilters];
    },
  };
}

export function createControlMock(name: string, options: ControlMockOptions = {}): Xrm.Controls.StandardControl {
  let label = options.label ?? name;
  let visible = options.visible ?? true;
  let disabled = options.disabled ?? false;
  const notifications = new Map<string, string>();
  const notificationLevels = new Map<string, Xrm.Controls.NotificationLevel>();

  const control = {
    getName: vi.fn(() => name),
    getControlType: vi.fn(() => options.controlType ?? "standard"),
    getAttribute: vi.fn(() => options.attribute ?? null),
    getLabel: vi.fn(() => label),
    setLabel: vi.fn((newLabel: string) => {
      label = newLabel;
    }),
    getVisible: vi.fn(() => visible),
    setVisible: vi.fn((newVisible: boolean) => {
      visible = newVisible;
    }),
    getDisabled: vi.fn(() => disabled),
    setDisabled: vi.fn((newDisabled: boolean) => {
      disabled = newDisabled;
    }),
    setNotification: vi.fn((message: string, uniqueId?: string) => {
      notifications.set(uniqueId ?? "", message);
      notificationLevels.set(uniqueId ?? "", "ERROR");
      return true;
    }),
    ...(options.addNotification === false
      ? {}
      : {
          addNotification: vi.fn((notification: Xrm.Controls.AddControlNotificationOptions) => {
            const id = notification.uniqueId ?? "";
            notifications.set(id, notification.messages[0] ?? "");
            notificationLevels.set(id, notification.notificationLevel ?? "ERROR");
          }),
        }),
    clearNotification: vi.fn((uniqueId?: string) => {
      if (uniqueId === undefined) {
        notifications.clear();
        notificationLevels.clear();
      } else {
        notifications.delete(uniqueId);
        notificationLevels.delete(uniqueId);
      }
      return true;
    }),
    setFocus: vi.fn(),
    ...choiceControlMembers(options.attribute),
    ...(options.lookup === true ? lookupControlMembers() : {}),
    ...(options.grid === undefined
      ? {}
      : {
          refresh: vi.fn(),
          getGrid: vi.fn(() => ({ getTotalRecordCount: vi.fn(() => options.grid!.recordCount) })),
        }),
    /** Test helper (not part of the Client API): current notifications keyed by unique id. */
    notifications,
    /** Test helper (not part of the Client API): level of each current notification. */
    notificationLevels,
  };

  return control as unknown as Xrm.Controls.StandardControl;
}

// ---------------------------------------------------------------------------------------------
// Form context
// ---------------------------------------------------------------------------------------------

export interface FormContextMockOptions {
  entityName?: string;
  entityId?: string;
  formType?: (typeof FormType)[keyof typeof FormType];
  /** Attributes by logical name. A control with the same name is created for each one. */
  attributes?: Record<string, AttributeMockOptions>;
  /**
   * Additional or overriding controls by name (e.g. subgrids, header controls). With `attributeName`
   * the control is bound to that column and joins its `controls`, like a second control of a column
   * (e.g. "telephone1_1" or "header_telephone1").
   */
  controls?: Record<string, ControlMockOptions & { attributeName?: string }>;
  /** Tabs by name, with their sections; `controls` lists control names placed in a section. */
  tabs?: Record<string, { visible?: boolean; sections?: Record<string, { visible?: boolean; controls?: string[] }> }>;
  /** Id returned by ui.formSelector.getCurrentItem().getId(). Omit to simulate a form without selector. */
  formId?: string;
}

function createVisibilityMock(name: string, visible: boolean, extra: object): Record<string, unknown> {
  let isVisible = visible;
  return {
    getName: vi.fn(() => name),
    getVisible: vi.fn(() => isVisible),
    setVisible: vi.fn((value: boolean) => {
      isVisible = value;
    }),
    ...extra,
  };
}

export function createFormContextMock(options: FormContextMockOptions = {}): Xrm.FormContext {
  const entityName = options.entityName ?? "";
  const entityId = options.entityId ?? (options.formType === FormType.Create ? "" : EMPTY_GUID);

  const attributes = Object.entries(options.attributes ?? {}).map(([name, attributeOptions]) =>
    createAttributeMock(name, attributeOptions),
  );

  const controlsByName = new Map<string, Xrm.Controls.StandardControl>();
  for (const attribute of attributes) {
    const { label, control: controlOptions } = options.attributes?.[attribute.getName()] ?? {};
    const control = createControlMock(attribute.getName(), { ...controlOptions, attribute, ...(label === undefined ? {} : { label }) });
    controlsByName.set(attribute.getName(), control);
    attributeControls.get(attribute)?.push(control);
  }
  for (const [name, { attributeName, ...controlOptions }] of Object.entries(options.controls ?? {})) {
    const attribute = attributeName === undefined ? undefined : attributes.find((a) => a.getName() === attributeName);
    const control = createControlMock(name, attribute === undefined ? controlOptions : { ...controlOptions, attribute });
    controlsByName.set(name, control);
    if (attribute !== undefined) attributeControls.get(attribute)?.push(control);
  }
  const controls = [...controlsByName.values()];

  const attributeCollection = createItemCollectionMock(attributes, (attribute) => attribute.getName());
  const controlCollection = createItemCollectionMock(controls, (control) => control.getName());
  const formNotifications = new Map<string, { message: string; level: Xrm.FormNotificationLevel }>();
  const saveHandlers: Xrm.Events.SaveEventHandler[] = [];
  const postSaveHandlers: Xrm.Events.PostSaveEventHandler[] = [];
  const dataLoadHandlers: Xrm.Events.DataLoadEventHandler[] = [];
  const remove = <T>(list: T[], item: T) => {
    const index = list.indexOf(item);
    if (index >= 0) list.splice(index, 1);
  };

  const tabs = Object.entries(options.tabs ?? {}).map(([tabName, tab]) => {
    const sections = Object.entries(tab.sections ?? {}).map(([sectionName, section]) =>
      createVisibilityMock(sectionName, section.visible ?? true, {
        controls: createItemCollectionMock(
          (section.controls ?? []).map((name) => controlsByName.get(name)).filter((c) => c !== undefined),
          (control) => control.getName(),
        ),
      }) as unknown as Xrm.Controls.Section,
    );
    let displayState: Xrm.DisplayState = "expanded";
    return createVisibilityMock(tabName, tab.visible ?? true, {
      sections: createItemCollectionMock(sections, (section) => section.getName()),
      getDisplayState: vi.fn(() => displayState),
      setDisplayState: vi.fn((state: Xrm.DisplayState) => {
        displayState = state;
      }),
      setFocus: vi.fn(),
    }) as unknown as Xrm.Controls.Tab;
  });

  const entity = {
    addOnSave: vi.fn((handler: Xrm.Events.SaveEventHandler) => {
      saveHandlers.push(handler);
    }),
    removeOnSave: vi.fn((handler: Xrm.Events.SaveEventHandler) => {
      const index = saveHandlers.indexOf(handler);
      if (index >= 0) saveHandlers.splice(index, 1);
    }),
    /** Test helper (not part of the Client API): handlers registered with addOnSave. */
    saveHandlers,
    addOnPostSave: vi.fn((handler: Xrm.Events.PostSaveEventHandler) => {
      postSaveHandlers.push(handler);
    }),
    removeOnPostSave: vi.fn((handler: Xrm.Events.PostSaveEventHandler) => remove(postSaveHandlers, handler)),
    /** Test helper (not part of the Client API): handlers registered with addOnPostSave. */
    postSaveHandlers,
    getEntityName: vi.fn(() => entityName),
    getId: vi.fn(() => entityId),
    getEntityReference: vi.fn(() => ({ entityType: entityName, id: entityId, name: "" })),
    getIsDirty: vi.fn(() => attributes.some((attribute) => attribute.getIsDirty())),
    getPrimaryAttributeValue: vi.fn(() => ""),
    isValid: vi.fn(() => true),
    save: vi.fn(),
    attributes: attributeCollection,
  };

  const formContext = {
    data: {
      entity,
      refresh: vi.fn(() => Promise.resolve()),
      save: vi.fn(() => Promise.resolve()),
      isValid: vi.fn(() => true),
      addOnLoad: vi.fn((handler: Xrm.Events.DataLoadEventHandler) => {
        dataLoadHandlers.push(handler);
      }),
      removeOnLoad: vi.fn((handler: Xrm.Events.DataLoadEventHandler) => remove(dataLoadHandlers, handler)),
      /** Test helper (not part of the Client API): handlers registered with data.addOnLoad. */
      dataLoadHandlers,
    },
    ui: {
      getFormType: vi.fn(() => options.formType ?? FormType.Update),
      setFormNotification: vi.fn((message: string, level: Xrm.FormNotificationLevel, uniqueId: string) => {
        formNotifications.set(uniqueId, { message, level });
        return true;
      }),
      clearFormNotification: vi.fn((uniqueId?: string) => {
        if (uniqueId === undefined) formNotifications.clear();
        else formNotifications.delete(uniqueId);
        return true;
      }),
      close: vi.fn(),
      refreshRibbon: vi.fn(),
      controls: controlCollection,
      tabs: createItemCollectionMock(tabs, (tab) => tab.getName()),
      formSelector: {
        getCurrentItem: vi.fn(() =>
          options.formId === undefined ? null : { getId: () => options.formId, getLabel: () => "Main" },
        ),
      },
      /** Test helper (not part of the Client API): current form notifications keyed by unique id. */
      notifications: formNotifications,
    },
    getAttribute: attributeCollection.get,
    getControl: controlCollection.get,
  } as unknown as Xrm.FormContext;

  for (const attribute of attributes) attributeOwners.set(attribute, formContext);
  return formContext;
}

// ---------------------------------------------------------------------------------------------
// Event context
// ---------------------------------------------------------------------------------------------

export interface EventContextMockOptions {
  eventSource?: Xrm.Attributes.Attribute | Xrm.Controls.Control | Xrm.Entity;
  depth?: number;
  eventArgs?: unknown;
  globalContext?: Xrm.GlobalContext;
}

export function createEventContextMock(
  formContext: Xrm.FormContext,
  options: EventContextMockOptions = {},
): Xrm.Events.EventContext {
  const sharedVariables = new Map<string, unknown>();

  return {
    getFormContext: vi.fn(() => formContext),
    getEventSource: vi.fn(() => options.eventSource ?? formContext.data.entity),
    getDepth: vi.fn(() => options.depth ?? 1),
    getContext: vi.fn(() => options.globalContext ?? ({} as Xrm.GlobalContext)),
    getEventArgs: vi.fn(() => options.eventArgs),
    getSharedVariable: vi.fn((key: string) => sharedVariables.get(key)),
    setSharedVariable: vi.fn((key: string, value: unknown) => {
      sharedVariables.set(key, value);
    }),
  } as unknown as Xrm.Events.EventContext;
}

/**
 * Another formContext object for the same form, sharing all its state. Dataverse does not promise
 * that every event handler gets the same formContext object, so code must not rely on its identity.
 */
export function rewrapFormContext(formContext: Xrm.FormContext): Xrm.FormContext {
  return { ...formContext } as Xrm.FormContext;
}

export interface UserSaveResult {
  /** Whether the OnSave handler ran at all. */
  readonly handlerRan: boolean;
  /** Whether the record would be saved. */
  readonly saved: boolean;
}

/**
 * A user's save, as Dataverse runs it: a control showing an error notification (setNotification, or
 * addNotification with level ERROR) blocks the save before OnSave runs; a RECOMMENDATION does not.
 * Otherwise OnSave runs with a fresh formContext object, and the save goes ahead unless the handler
 * calls preventDefault().
 */
export function simulateUserSave(formContext: Xrm.FormContext, onSave: (context: Xrm.Events.SaveEventContext) => void): UserSaveResult {
  const controls = formContext.ui.controls.get() as unknown as { notificationLevels?: Map<string, Xrm.Controls.NotificationLevel> }[];
  const blocked = controls.some((control) => [...(control.notificationLevels?.values() ?? [])].includes("ERROR"));
  if (blocked) return { handlerRan: false, saved: false };

  const context = createSaveEventContextMock(rewrapFormContext(formContext));
  onSave(context);
  return { handlerRan: true, saved: !context.getEventArgs().isDefaultPrevented() };
}

/**
 * Simulates the form data loading again (after a save or refresh): runs the data OnLoad handlers with a
 * fresh formContext object, as Dataverse does.
 */
export function fireDataLoad(formContext: Xrm.FormContext): void {
  const handlers = (formContext.data as unknown as { dataLoadHandlers: Xrm.Events.DataLoadEventHandler[] }).dataLoadHandlers;
  const context = createEventContextMock(rewrapFormContext(formContext)) as unknown as Xrm.Events.DataLoadEventContext;
  [...handlers].forEach((handler) => handler(context));
}

/** OnSave context whose event arguments remember preventDefault(), like the real save event. */
export function createSaveEventContextMock(formContext: Xrm.FormContext, saveMode = 1): Xrm.Events.SaveEventContext {
  let prevented = false;
  const eventArgs = {
    getSaveMode: vi.fn(() => saveMode),
    isDefaultPrevented: vi.fn(() => prevented),
    preventDefault: vi.fn(() => {
      prevented = true;
    }),
    preventDefaultOnError: vi.fn(),
  };

  return createEventContextMock(formContext, { eventArgs }) as unknown as Xrm.Events.SaveEventContext;
}
