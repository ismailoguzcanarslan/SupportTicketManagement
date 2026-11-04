// Lookup column operations. Single-value lookups only; use Field for multi-table lookups.

import { normalizeId } from "./xrm.js";

export const Lookup = {
  /** First lookup value, or null when empty or not on the form. */
  getValue(formContext: Xrm.FormContext, name: string): Xrm.LookupValue | null {
    const value = formContext.getAttribute<Xrm.Attributes.LookupAttribute>(name)?.getValue();
    return value?.[0] ?? null;
  },

  /** Referenced record id without braces. */
  getId(formContext: Xrm.FormContext, name: string): string | null {
    const value = Lookup.getValue(formContext, name);
    return value ? normalizeId(value.id) : null;
  },

  /** Referenced table logical name, e.g. "contact". */
  getEntityType(formContext: Xrm.FormContext, name: string): string | null {
    return Lookup.getValue(formContext, name)?.entityType ?? null;
  },

  getName(formContext: Xrm.FormContext, name: string): string | null {
    return Lookup.getValue(formContext, name)?.name ?? null;
  },

  /** Sets or clears (null) the lookup. Returns false when the column is not on the form. */
  setValue(formContext: Xrm.FormContext, name: string, value: { id: string; entityType: string; name?: string } | null): boolean {
    const attribute = formContext.getAttribute<Xrm.Attributes.LookupAttribute>(name);
    if (attribute === null) return false;

    attribute.setValue(value === null ? null : [{ id: normalizeId(value.id), entityType: value.entityType, name: value.name ?? "" }]);
    return true;
  },
} as const;
