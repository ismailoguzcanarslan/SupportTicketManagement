// Form-level operations on a formContext.

import { normalizeId, toPromise } from "./xrm.js";

/** XrmEnum.FormType values (ambient const enums cannot be used with isolatedModules). */
export const FormType = {
  Undefined: 0,
  Create: 1,
  Update: 2,
  ReadOnly: 3,
  Disabled: 4,
  BulkEdit: 6,
} as const;

export const Form = {
  /** Id of the form currently shown, or null when the form selector is unavailable (e.g. quick create). */
  getFormId(formContext: Xrm.FormContext): string | null {
    const id = formContext.ui.formSelector?.getCurrentItem()?.getId();
    return id ? normalizeId(id) : null;
  },

  getFormType(formContext: Xrm.FormContext): number {
    return formContext.ui.getFormType();
  },

  isCreate(formContext: Xrm.FormContext): boolean {
    return formContext.ui.getFormType() === FormType.Create;
  },

  getEntityName(formContext: Xrm.FormContext): string {
    return formContext.data.entity.getEntityName();
  },

  /** Record id without braces, or null for a record that is not saved yet. */
  getEntityId(formContext: Xrm.FormContext): string | null {
    const id = formContext.data.entity.getId();
    return id ? normalizeId(id) : null;
  },

  save(formContext: Xrm.FormContext, options?: Xrm.SaveOptions): Promise<void> {
    const saved = options === undefined ? formContext.data.save() : formContext.data.save(options);
    return toPromise(saved).then(() => undefined);
  },

  /** Reloads form data. Pass `save: true` to save pending changes first. */
  refresh(formContext: Xrm.FormContext, save = false): Promise<void> {
    return toPromise(formContext.data.refresh(save)).then(() => undefined);
  },
} as const;
